using System.Text.Json.Serialization;

namespace MadWizard.Desomnia.Network.FRITZ.API.Model
{
    /// <summary>Envelope of <c>GET /api/v0/generic/eth_ports</c>: <c>{ "eth": [ … ] }</c>.</summary>
    public sealed class EthernetPortList
    {
        [JsonPropertyName("eth")] public List<EthernetPort> Eth { get; set; } = [];
    }
}
