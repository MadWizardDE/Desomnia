using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Manager
{
    /// <summary>
    /// Identity of a network interface — the network counterpart of DisplayIdentity.
    ///
    /// Today the identity is the OS interface id alone (a GUID string on Windows, the
    /// interface name on the Unixes), which is unique among the living interfaces since
    /// no interface is ever watched more than once. The struct exists so consumers key
    /// on the concept rather than a bare string: should more identity information ever
    /// become necessary, it is added here — and every key keeps its meaning.
    /// </summary>
    public readonly record struct NetworkIdentity(string Id)
    {
        public override string ToString() => Id;
    }

    public static class NetworkInterfaceExtensions
    {
        public static NetworkIdentity ToIdentity(this NetworkInterface @interface) => new(@interface.Id);
    }
}
