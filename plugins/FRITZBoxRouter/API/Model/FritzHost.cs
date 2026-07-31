using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.FRITZ.API.Model
{
    /// <summary>
    /// One entry from the box' known-host table (TR-064 <c>X_AVM-DE_GetHostListPath</c> →
    /// <c>devicehostlist.lua</c>). This is the authoritative source for MAC addresses: it lists
    /// every host the box has ever leased, connected or not, plus the VPN peers (which are
    /// layer-3 only, hence <see cref="MAC"/> is null and <see cref="IsVPN"/> is set).
    /// </summary>
    public sealed class FritzHost
    {
        public required string Name { get; init; }

        public string InterfaceType { get; init; } = "";

        public IPAddress? IP { get; init; }
        public PhysicalAddress? MAC { get; init; }

        public bool IsActive { get; init; }
        /// <summary>True for a VPN peer — a virtual, MAC-less host with a fixed tunnel IP.</summary>
        public bool IsVPN { get; init; }

        public override string ToString()
            => $"{Name} [{IP?.ToString() ?? "-"} {MAC?.ToString() ?? "--"}]{(IsVPN ? " (VPN)" : "")}{(IsActive ? " active" : "")}";
    }
}
