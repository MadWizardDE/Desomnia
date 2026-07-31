using System.Text.Json.Serialization;

namespace MadWizard.Desomnia.Network.FRITZ.API.Model
{
    /// <summary>
    /// One ethernet port as returned by <c>GET /api/v0/generic/eth_ports</c>. The box reports
    /// every field as a string (including the numbers). <see cref="Uid"/> is the stable handle
    /// used to address the port on <c>PUT …/eth_ports/eth/{UID}</c>.
    /// </summary>
    public sealed class EthernetPort
    {
        [JsonPropertyName("ifname")]     public string IfName    { get; set; } = "";
        [JsonPropertyName("UID")]        public string Uid       { get; set; } = "";
        [JsonPropertyName("label")]      public string Label     { get; set; } = "";
        [JsonPropertyName("function")]   public string Function  { get; set; } = "";

        /// <summary>Allowed maxspeed values as a comma list, e.g. <c>"10,100,1000,2500"</c>.</summary>
        [JsonPropertyName("speed_list")] public string SpeedList { get; set; } = "";

        /// <summary>Live negotiated link speed in Mbit; <c>"0"</c> when the link is down.</summary>
        [JsonPropertyName("speed")]      public string Speed     { get; set; } = "";

        /// <summary>Configured speed cap in Mbit — the value a maxspeed action changes.</summary>
        [JsonPropertyName("maxspeed")]   public string MaxSpeed  { get; set; } = "";

        /// <summary>Energy Efficient Ethernet mode; sent back alongside a maxspeed change.</summary>
        [JsonPropertyName("eee_mode")]   public string EeeMode   { get; set; } = "";

        /// <summary><c>"1"</c> when a cable is connected.</summary>
        [JsonPropertyName("carrier")]    public string Carrier   { get; set; } = "";

        /// <summary>The <see cref="SpeedList"/> parsed to integers.</summary>
        public IEnumerable<int> AllowedSpeeds
            => SpeedList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => int.TryParse(s, out var v) ? v : -1)
                        .Where(v => v > 0);

        public override string ToString() => $"{Label} ({IfName}, {Function}) maxspeed={MaxSpeed}/{SpeedList}";
    }
}
