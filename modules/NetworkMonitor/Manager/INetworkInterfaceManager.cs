using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Manager
{
    /// <summary>
    /// Platform view of this machine's network interfaces.
    ///
    /// Enumeration yields every interface the manager still knows: the ones present in the OS
    /// enumeration plus any detached one a standing
    /// <see cref="INetworkInterface.ShouldBeDisabled"/> intent keeps alive, so the intent can
    /// be released even after the interface is gone. A disabled interface is not one of those
    /// detached ones — it stays in the OS enumeration, reading (like a merely disconnected one)
    /// as <see cref="OperationalStatus.Down"/>.
    ///
    /// Identity guarantee: the manager remembers a detached interface for as long as anyone
    /// still references the instance (weakly), and a return of the same
    /// <see cref="NetworkIdentity"/> resurfaces THE SAME instance through
    /// <see cref="InterfaceAttached"/>. Upper layers can rely on reference equality alone;
    /// only once the last reference is collected does a return produce a new instance.
    /// </summary>
    public interface INetworkInterfaceManager : IIEnumerable<INetworkInterface>
    {
        /// <summary>The present interface with this identity — null when none is currently in
        /// the OS enumeration (a held-but-vanished one is not returned here).</summary>
        INetworkInterface? this[NetworkIdentity identity] { get; }

        event EventHandler<INetworkInterface> InterfaceAttached;
        event EventHandler<INetworkInterface> InterfaceDetached;

        /// <summary>Any observed change (attach/detach/address/status) — a coarse re-plan trigger.</summary>
        event EventHandler? Changed;
    }
}
