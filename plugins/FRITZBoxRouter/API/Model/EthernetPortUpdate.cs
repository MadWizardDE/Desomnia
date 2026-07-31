using System.Text.Json.Serialization;

namespace MadWizard.Desomnia.Network.FRITZ.API.Model
{
    /// <summary>
    /// Body of <c>PUT /api/v0/generic/eth_ports/eth/{UID}</c>. Mirrors exactly what the web UI
    /// sends: <c>maxspeed</c> as a number, <c>eee_mode</c> as a string.
    /// </summary>
    public sealed class EthernetPortUpdate
    {
        [JsonPropertyName("maxspeed")] public int    MaxSpeed { get; set; }
        [JsonPropertyName("eee_mode")] public string EeeMode  { get; set; } = "";
    }
}
