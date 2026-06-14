using Autofac;
using MadWizard.Desomnia.Network.Filter;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network
{
    public class NetworkDevice : IDisposable
    {
        public ILogger<NetworkDevice> Logger { private get; init; }

        public  string              Name => Device.Description ?? Device.Name;
        public  NetworkInterface    Interface   { get; internal set; }
        private ILiveDevice         Device      { get; init; }

        public bool IsCapturing => Device.Started;

        public bool IsMaxResponsiveness;
        public bool IsNoCaptureLocal;

        public string? Filter
        {
            get => Device.Filter;

            set
            {
                if (Device.Filter != value)
                {
                    var runtime = Device.Started;

                    if (runtime)
                    {
                        Logger.LogDebug("BPF rule = '{expr}'", value);

                        Device.OnCaptureStopped -= Device_OnCaptureStopped;
                        Device.StopCapture();
                    }

                    Device.Filter = value;

                    if (runtime)
                    {
                        Device.StartCapture();
                        Device.OnCaptureStopped += Device_OnCaptureStopped;
                    }
                }
            }
        }
        public IEnumerable<IDevicePacketFilter> Filters { private get; init; } = [];

        public PhysicalAddress PhysicalAddress => Interface.GetPhysicalAddress() ?? PhysicalAddress.None;

        public IEnumerable<IPAddress> IPAddresses
        {
            get
            {
                IEnumerable<IPAddress> pcapAddresses = [];
                IEnumerable<IPAddress> niAddresses = [];

                if (Device is LibPcapLiveDevice pcap)
                {
                    pcapAddresses = pcap.Addresses
                        .Where(address => address.Addr?.ipAddress is not null)
                        .Select(address => address.Addr?.ipAddress!);
                }

                niAddresses = Interface.GetIPProperties().UnicastAddresses
                    .Where(unicast => unicast.Address is not null)
                    .Select(unicast => unicast.Address);

                return pcapAddresses.Concat(niAddresses).Select(IPAddressExt.RemoveScopeId).Distinct();
            }
        }

        public IPAddress? IPv4Address => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
        public IPAddress? IPv6LinkLocalAddress => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv6LinkLocal).FirstOrDefault();
        public IEnumerable<IPAddress> IPv6Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);

        public IPAddress? IPv6LinkLocalMulticastAddress
        {
            get
            {
                if (IPv6Addresses.Any())
                {
                    int scopeId = Interface.GetIPProperties().GetIPv6Properties().Index;

                    return new IPAddress(IPAddressExt.LinkLocalMulticast.GetAddressBytes(), scopeId);
                }

                return null;
            }
        }

        public event EventHandler<EthernetPacket>? EthernetCaptured;

        internal BlockingCollection<RawCapture>? PacketQueue { get; private set; }
        internal Thread? ProcessingThread { get; private set; }

        public NetworkDevice(ILogger<NetworkDevice> logger, NetworkInterface @interface, ILiveDevice device)
        {
            Logger = logger;

            Interface = @interface;
            Device = device;

            if (!TryOpen(Device, ref IsMaxResponsiveness, ref IsNoCaptureLocal))
            {
                throw new Exception($"Failed to open network device \"{Name}\"");
            }
        }

        public bool HasSentPacket(EthernetPacket packet)
        {
            return this.PhysicalAddress.Equals(packet.SourceHardwareAddress); // TODO will this work with virtual interfaces? (OpenVPN)
        }

        public void StartCapture()
        {
            PacketQueue = [];

            ProcessingThread = new Thread(ProcessQueuedPackets)
            {
                Name = $"PacketProcessor:{Name}",
                IsBackground = true
            };
            ProcessingThread.Start();

            Device.OnPacketArrival += Device_OnPacketArrival;
            Device.StartCapture();
            Device.OnCaptureStopped += Device_OnCaptureStopped;

            List<string> features = [];
            if (IsMaxResponsiveness)
                features.Add("MaxResponsiveness");
            if (IsNoCaptureLocal)
                features.Add("NoCaptureLocal");

            var countIPv6 = IPv6Addresses.Count();

            Logger.LogInformation($"Capturing network device \"{Name}\"; MAC={PhysicalAddress?.ToHexString()}, IPv4={IPv4Address?.ToString() ?? "?"}" +
                (countIPv6 > 0 ? $", IPv6={IPv6LinkLocalAddress?.ToString() ?? IPv6Addresses.FirstOrDefault()?.ToString() ?? "?"}" + (countIPv6 - 1 > 0 ? $"(+{countIPv6 - 1})" : "") : "") +
                $" [{string.Join(", ", features)}]");

            if (Filter != null)
            {
                Logger.LogDebug("BPF rule = '{expr}'", Filter);
            }
        }

        /// <summary>
        /// Capture-thread callback. To respect libpcap's single-threaded handle requirement and to
        /// keep draining the kernel buffer, this does the bare minimum on the capture thread: filter
        /// out our own injected packets, copy the bytes out of the libpcap-owned buffer and hand them
        /// to the queue. Parsing and dispatch happen later, serially, on the processing thread.
        /// </summary>
        private void Device_OnPacketArrival(object sender, PacketCapture capture)
        {
            try
            {
                if (!FilterInjectedPacket(capture))
                {
                    var raw = capture.GetPacket();

                    try
                    {
                        if (!PacketQueue?.TryAdd(raw) ?? false)
                        {
                            Logger.LogWarning("Could not enqueue a captured packet."); // should not happen, since queue is unbounded
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // The queue was completed concurrently during shutdown; nothing to do.
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while filtering/queuing packet."); // low level error
            }
        }

        private void Device_OnCaptureStopped(object sender, CaptureStoppedEventStatus status)
        {
            Logger.Log(status == CaptureStoppedEventStatus.ErrorWhileCapturing ? LogLevel.Error : LogLevel.Warning, 
                "Packet capturing stopped."); // let's see if this happens
        }

        /// <summary>
        /// The single consumer: drains the user-space buffer and dispatches each packet in arrival
        /// order via <see cref="EthernetCaptured"/>. Running off the capture thread means a slow
        /// handler no longer stalls capture or overflows the kernel ring buffer.
        /// </summary>
        private void ProcessQueuedPackets()
        {
            try
            {
                foreach (var raw in PacketQueue!.GetConsumingEnumerable())
                {
                    try
                    {
                        if (Packet.ParsePacket(raw.LinkLayerType, raw.Data) is EthernetPacket ethernet)
                        {
                            try
                            {
                                EthernetCaptured?.Invoke(this, ethernet);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "Error processing packet:\n{packet}", ethernet.ToTraceString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error parsing packet.");
                    }
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                // Queue was disposed/completed while we were blocked in GetConsumingEnumerable; exit.
            }
        }

        private bool FilterInjectedPacket(PacketCapture capture)
        {
            foreach (var filter in Filters)
            {
                if (filter.FilterIncoming(capture))
                {
                    return true;
                }
            }

            return false;
        }

        private static void PreparePacketToSend(EthernetPacket packet)
        {
            if (packet.Extract<UdpPacket>() is UdpPacket udp)
            {
                udp.UpdateCalculatedValues();
                udp.UpdateUdpChecksum();
            }

            if (packet.Extract<IPPacket>() is IPPacket ip)
            {
                ip.UpdateCalculatedValues();

                if (ip is IPv4Packet ipv4Packet)
                    ipv4Packet.UpdateIPChecksum();
            }
        }

        public void SendPacket(EthernetPacket packet, bool prepare = false)
        {
            if (prepare)
            {
                PreparePacketToSend(packet);
            }

            try
            {
                if (!Filters.Select(filter => filter.FilterOutgoing(packet)).Where(f => f == true).Any())
                {
                    if (Logger.IsEnabled(LogLevel.Trace))
                    {
                        Logger.LogTrace($"SEND PACKET\n{packet.ToTraceString()}");
                    }

                    lock (Device)
                    {
                        Device.SendPacket(packet);
                    }
                }
            }
            catch (DeviceNotReadyException ex)
            {
                Logger.LogWarning(ex, "");
            }
        }

        private async Task<bool> UntilFullyOperational()
        {
            const int MAX_RETRIES = 16;
            const int WAIT_TIME = 500;

            int retry = 0;

            while (true)
            {
                try
                {
                    lock (Device)
                    {
                        Device.SendPacket(new EthernetPacket(PhysicalAddressExt.Empty, PhysicalAddressExt.Empty, EthernetType.WakeOnLan)
                        {
                            PayloadPacket = new WakeOnLanPacket(PhysicalAddressExt.Empty)
                        });
                    }

                    return true;
                }
                catch (PcapException ex)
                {
                    if (retry++ == 0)
                    {
                        Logger.LogTrace($"Network device \"{Name}\" is not yet fully operational. Waiting up to {MAX_RETRIES * WAIT_TIME / 1000} seconds...");
                    }
                    else if (retry >= MAX_RETRIES)
                    {
                        Logger.LogError(ex, $"Network interface \"{Name}\" has not become fully operational.");

                        return false;
                    }

                    await Task.Delay(WAIT_TIME);
                }
            }
        }

        internal void Restart()
        {
            StopCapture();

            var filter = Filter;

            lock (Device)
            {
                Device.Close();
            }

            TryOpen(Device, ref IsMaxResponsiveness, ref IsNoCaptureLocal);

            Filter = filter;

            StartCapture();
        }

        public void StopCapture()
        {
            if (!Device.Started)
                return;

            Device.OnCaptureStopped -= Device_OnCaptureStopped;
            Device.StopCapture();
            Device.OnPacketArrival -= Device_OnPacketArrival;

            PacketQueue?.CompleteAdding();
            ProcessingThread?.Join(TimeSpan.FromSeconds(5));
            PacketQueue?.Dispose();
            PacketQueue = null;

            ProcessingThread = null;

            Logger.LogInformation($"Stopped capturing network device \"{Name}\"");
        }

        private bool TryOpen(ILiveDevice device, ref bool maxResponsiveness, ref bool noCaptureLocal)
        {
            try
            {
                device.Open(DeviceModes.Promiscuous | DeviceModes.MaxResponsiveness | DeviceModes.NoCaptureLocal);

                maxResponsiveness = true;
                noCaptureLocal = true;

                return true;
            }
            catch (PcapException)
            {
                Logger.LogDebug($"Device '{Name}' does not support NoCaptureLocal mode. Compensating with fallback buffer.");
            }

            noCaptureLocal = false; // not supported

            try
            {
                device.Open(DeviceModes.Promiscuous | DeviceModes.MaxResponsiveness);

                maxResponsiveness = true;

                return true;
            }
            catch (PcapException)
            {
                Logger.LogWarning($"Device '{Name}' does not support MaxResponsiveness mode. Anticipate slow application behavior.");
            }

            maxResponsiveness = false; // not supported

            try
            {
                device.Open(DeviceModes.Promiscuous);

                return true;
            }
            catch (PcapException)
            {
                Logger.LogError($"Device '{Name}' does not support Promiscuous mode.");
            }

            return false; // at least promiscuous mode is needed
        }

        void IDisposable.Dispose()
        {
            if (IsCapturing)
            {
                StopCapture();
            }

            lock (Device)
            {
                Device.Close();
            }
        }
    }

    internal class ContentionPacketFilter(NetworkDevice device) : IDevicePacketFilter
    {
        // User-space buffer between the capture thread (producer) and a single processing thread
        // (consumer). Decoupling them means a slow packet handler can no longer block capture or
        // overflow the kernel ring buffer, while packets are still dispatched strictly in order.
        const int QueueLimit = 4096;
        const int DropWarningIntervalMs = 5000;

        public required ILogger<ContentionPacketFilter> Logger { private get; init; }

        private DateTime _lastWarning = DateTime.MinValue;

        private int _droppedSinceWarning;

        bool IDevicePacketFilter.FilterIncoming(PacketCapture packet)
        {
            if (device.PacketQueue?.Count > QueueLimit)
            {
                _droppedSinceWarning++;

                var now = DateTime.UtcNow;
                if (now - _lastWarning > TimeSpan.FromMilliseconds(DropWarningIntervalMs))
                {
                    Logger.LogWarning("Processing queue for \"{Name}\" is saturated; dropped {Count} packet(s) in user space because processing can't keep up.",
                        device.Name, _droppedSinceWarning);

                    _droppedSinceWarning = 0;
                    _lastWarning = now;
                }

                return true;
            }

            return false;
        }

        bool IDevicePacketFilter.FilterOutgoing(Packet packet)
        {
            return false;
        }
    }

    /// <summary>
    /// This Filter prevent packets sent by us, being processed as incoming packets again,
    /// if the device is cannot do this by itself.
    /// </summary>
    /// 
    /// <param name="device">the monitored network device</param>
    internal class LocalPacketFilter(NetworkDevice device) : IDevicePacketFilter
    {
        private readonly IList<byte[]> _sentPackets = [];

        public bool FilterIncoming(PacketCapture packet)
        {
            if (device.IsNoCaptureLocal)
                return false;

            lock (_sentPackets)
            {
                foreach (var bytes in _sentPackets)
                    if (packet.Data.SequenceEqual(bytes))
                        return _sentPackets.Remove(bytes);
            }

            return false;
        }

        public bool FilterOutgoing(Packet packet)
        {
            if (device.IsNoCaptureLocal)
                return false;

            lock (_sentPackets)
            {
                _sentPackets.Add(packet.Bytes);
            }

            return false;
        }
    }

    internal class SimulationPacketFilter : IDevicePacketFilter
    {
        public required ILogger<SimulationPacketFilter> Logger { private get; init; }

        public bool FilterIncoming(PacketCapture packet) => false;

        public bool FilterOutgoing(Packet packet) => true;
    }
}
