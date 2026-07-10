using MadWizard.Desomnia.Network.Datagram;
using MadWizard.Desomnia.Network.Naming.Options;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Naming
{
    public abstract class DNSService(ushort port, string? realm = null) : DatagramService(port, realm)
    {
        protected override void ProcessDatagram(DatagramPacket datagram)
        {
            try
            {
                var message = (Message)new Message().Read(datagram.Payload);

                ProcessMessage(datagram, message);
            }
            catch (Exception ex)
            {
                WireLogger.LogTrace(ex, "Failed to parse a datagram on port {Port} as a DNS message.", Port);
            }
        }

        protected virtual void ProcessMessage(DatagramPacket source, Message message)
        {
            try
            {
                if (message.IsQuery)
                {
                    switch (message.Opcode)
                    {
                        case MessageOperation.Query:
                            DNSQuery query = new(source, message);
                            ProcessQuery(query);
                            break;

                        case MessageOperation.Update:
                            DNSUpdate update = new(source, message);
                            ProcessUpdate(update);
                            break;
                    }
                }
                else
                {
                    ProcessResponse(message);
                }
            }
            catch (Exception ex)
            {
                WireLogger.LogTrace(ex, "Failed to process a DNS message on port {Port} [{ID}]", Port, message.Id);
            }
        }

        protected virtual void ProcessQuery(DNSQuery query)     { }
        protected virtual void ProcessUpdate(DNSUpdate update)  { }
        protected virtual void ProcessResponse(Message message) { }

        /// <summary>
        /// Sends the accumulated answers for <paramref name="query"/> back to the link -- out the
        /// same inlet the query came in: via the OS socket, or as a crafted frame. A crafted reply
        /// is multicast to the all-mDNS group; but when the query may be answered by sendUnicast
        /// (every question has the QU bit set) or a debugger is attached, it is sent straight back
        /// to the querier instead.
        /// </summary>
        protected void RespondTo(DNSQuery query)
        {
            using var scope = WireLogger.BeginRealmScope(Realm);

            try
            {
                if (query.Packet.TryRespond(query.Response.ToByteArray()))
                    return;

                IPPacket ip = query.SourceIPAddress.AddressFamily == AddressFamily.InterNetwork
                    ? new IPv4Packet(query.TargetIPAddress, query.SourceIPAddress) { TimeToLive = 255 }
                    : new IPv6Packet(query.TargetIPAddress, query.SourceIPAddress) { HopLimit = 255 };

                ip.PayloadPacket = new UdpPacket(Port, query.SourcePort)
                {
                    PayloadData = query.Response.ToByteArray()
                };

                EthernetPacket packet = new(Device.PhysicalAddress, query.SourcePhysicalAddress, EthernetType.None) // EtherType = auto
                {
                    PayloadPacket = ip
                };

                RespondWith(query, packet);
            }
            catch (Exception ex)
            {
                WireLogger.LogTrace(ex, "Failed to send a DNS response from port {Port} [{ID}]", Port, query.Request.Id);
            }
        }

        protected virtual void RespondWith(DNSQuery query, EthernetPacket packet)
        {
            Device.SendPacket(packet, true);
        }

        /// <summary>Register the sleep-proxy EDNS0 options codes used by the Bonjour Sleep Proxy registration that Makaretu doesn't know about.</summary>
        static DNSService()
        {
            EdnsOptionRegistry.Register<EdnsLeaseOption>();
            EdnsOptionRegistry.Register<EdnsOwnerOption>();

            EdnsOptionRegistry.Register<EdnsServiceFilterOption>();
            EdnsOptionRegistry.Register<EdnsPagingOption>();
        }
    }
}
