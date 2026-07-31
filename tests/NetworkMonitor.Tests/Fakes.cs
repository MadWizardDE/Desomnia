using MadWizard.Desomnia.Network.Manager;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Tests
{
    /// <summary>Just enough <see cref="INetworkInterface"/> for matchers and planning —
    /// an id-keyed handle with settable observations and inert intent flags.</summary>
    internal class FakeNetworkInterface(string id) : INetworkInterface
    {
        public NetworkIdentity Identity { get; } = new(id);

        public string Name { get; init; } = id;

        public OperationalStatus Status { get; set; } = OperationalStatus.Up;

        public NetworkInterfaceType Type { get; set; } = NetworkInterfaceType.Ethernet;

        public PhysicalAddress PhysicalAddress { get; set; } = PhysicalAddress.None;

        public IReadOnlyList<UnicastIPAddressInformation> Addresses { get; set; } = [];

        public IReadOnlyList<GatewayIPAddressInformation> Gateways { get; set; } = [];

        public string? DNSSuffix { get; set; }

        public int? IPv6ScopeIndex { get; set; }

        public virtual string? SSID { get; set; }

        public bool ShouldBeDisabled { get; set; }

        public bool EnforceDisabled { get; set; }

        public override string ToString() => Name;
    }

    /// <summary>A fixed roster of interfaces instead of a live OS enumeration.</summary>
    internal sealed class FakeNetworkInterfaceManager(params INetworkInterface[] interfaces) : INetworkInterfaceManager
    {
        private readonly List<INetworkInterface> _interfaces = [.. interfaces];

        private EventHandler? _changed;

        public INetworkInterface? this[NetworkIdentity identity] => _interfaces.FirstOrDefault(i => i.Identity == identity);

        public event EventHandler<INetworkInterface>? InterfaceAttached;
        public event EventHandler<INetworkInterface>? InterfaceDetached;

        public event EventHandler? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public bool HasChangedSubscribers => _changed is not null;

        public void RaiseChanged() => _changed?.Invoke(this, EventArgs.Empty);

        public void Attach(INetworkInterface @interface)
        {
            _interfaces.Add(@interface);

            InterfaceAttached?.Invoke(this, @interface);

            RaiseChanged();
        }

        public void Detach(INetworkInterface @interface)
        {
            _interfaces.Remove(@interface);

            InterfaceDetached?.Invoke(this, @interface);

            RaiseChanged();
        }

        IEnumerator<INetworkInterface> IEnumerable<INetworkInterface>.GetEnumerator() => _interfaces.GetEnumerator();
    }
}
