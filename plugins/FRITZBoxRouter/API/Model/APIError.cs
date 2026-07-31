using System.Text.Json.Serialization;

namespace MadWizard.Desomnia.Network.FRITZ.API.Model
{
    /// <summary>Error envelope the REST API returns, e.g. an expired session id:
    /// <c>{ "errors": [ { "code": 3001, "message": "permission denied: …" } ] }</c>.</summary>
    public sealed class APIErrorResponse
    {
        [JsonPropertyName("errors")] public List<APIError>? Errors { get; set; }
    }

    public sealed class APIError
    {
        /// <summary>3001 = permission denied (session invalid/expired or insufficient rights).</summary>
        [JsonPropertyName("code")]    public int     Code    { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
