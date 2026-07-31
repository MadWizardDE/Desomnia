using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Manager
{
    /// <summary>
    /// A network interface of this machine, the platform-independent handle handed out by the
    /// <see cref="INetworkInterfaceManager"/>. On disappearance the manager raises
    /// <see cref="INetworkInterfaceManager.InterfaceDetached"/>; the instance stays valid, and
    /// as long as anyone still holds a reference to it — or a standing
    /// <see cref="ShouldBeDisabled"/> intent keeps it — a returning interface with the same
    /// <see cref="NetworkIdentity"/> resurfaces as THE SAME instance (see
    /// <see cref="INetworkInterfaceManager"/>). Consumers therefore never need any identity
    /// check beyond reference equality.
    /// </summary>
    public interface INetworkInterface
    {
        /// <summary>The detached identity — <see cref="NetworkIdentity.Id"/> is the OS
        /// interface id (a GUID string on Windows, the interface name on the Unixes).</summary>
        NetworkIdentity Identity { get; }

        /// <summary>The human-readable name: the display name on Windows ("Ethernet 2"),
        /// the same as the id on the Unixes ("en0", "eth0").</summary>
        string Name { get; }

        /// <summary>
        /// The operational status the OS reports, retaining its last value after the interface
        /// leaves the enumeration. Note it does NOT tell an administratively disabled interface
        /// apart from a merely disconnected one — both read as <see cref="OperationalStatus.Down"/>
        /// (a WiFi adapter especially). The manager keeps that distinction internally, so it
        /// restores only an interface it actually took out of service.
        /// </summary>
        OperationalStatus Status { get; }

        NetworkInterfaceType Type { get; }

        /// <summary><see cref="PhysicalAddress.None"/> when unknown.</summary>
        PhysicalAddress PhysicalAddress { get; }

        /// <summary>The unicast addresses of the interface, as the OS reports them — an IPv6
        /// link-local address therefore still carries its scope; the interface scope index is
        /// also available on its own in <see cref="IPv6ScopeIndex"/>.</summary>
        IReadOnlyList<UnicastIPAddressInformation> Addresses { get; }

        /// <summary>The gateway addresses of the interface, as the OS reports them.</summary>
        IReadOnlyList<GatewayIPAddressInformation> Gateways { get; }

        string? DNSSuffix { get; }

        /// <summary>The interface index for IPv6 scoping; null when the interface has no IPv6.</summary>
        int? IPv6ScopeIndex { get; }

        /// <summary>
        /// The wireless network this interface is joined to, when connected; null otherwise.
        /// </summary>
        /// <exception cref="NotSupportedException">The platform exposes no wireless
        /// information (only a platform host's manager can answer this).</exception>
        string? SSID { get; }

        /// <summary>
        /// OUR declared intent to keep this interface out of service — intent, not observed
        /// state. Setting it routes to the manager, which owns locking and reconciliation:
        /// the disable is applied while the interface is present and re-applied when it
        /// returns, and only a state we actually took away is restored on release. The
        /// intent keeps the handle alive across a disconnection, so it survives even
        /// where disabling removes the interface from the OS enumeration (Windows).
        /// </summary>
        bool ShouldBeDisabled { get; set; }

        /// <summary>
        /// While <see cref="ShouldBeDisabled"/>: whether a foreign re-enable is answered by
        /// disabling the interface again. The default (false) is tolerant — a user flipping
        /// the interface back on wins until the next intent change.
        /// </summary>
        bool EnforceDisabled { get; set; }
    }
}
