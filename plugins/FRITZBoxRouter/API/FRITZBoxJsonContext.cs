using MadWizard.Desomnia.Network.FRITZ.API.Model;
using System.Text.Json.Serialization;

namespace MadWizard.Desomnia.Network.FRITZ.API
{
    /// <summary>
    /// Source-generated (de)serialization metadata for the REST DTOs — keeps the plugin
    /// reflection-free so it survives the NativeAOT/trimmed builds Desomnia ships.
    /// </summary>
    [JsonSerializable(typeof(EthernetPortList))]
    [JsonSerializable(typeof(EthernetPort))]
    [JsonSerializable(typeof(EthernetPortUpdate))]
    [JsonSerializable(typeof(APIErrorResponse))]
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
    internal partial class FRITZBoxJsonContext : JsonSerializerContext { }
}
