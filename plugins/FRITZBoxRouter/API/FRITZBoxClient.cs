using MadWizard.Desomnia.Network.FRITZ.API.Model;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MadWizard.Desomnia.Network.FRITZ.API
{
    /// <summary>
    /// Thin, AOT-safe HTTP client for a single FRITZ!Box. It bridges two APIs that share one
    /// credential:
    /// <list type="bullet">
    /// <item>TR-064 SOAP on :49000 (HTTP digest) — used only to mint a web session id via
    /// <c>DeviceConfig#X_AVM-DE_CreateUrlSID</c>.</item>
    /// <item>The FRITZ!OS REST API <c>/api/v0/…</c> — authorized with the header
    /// <c>Authorization: AVM-SID &lt;sid&gt;</c>. This is where port speeds live.</item>
    /// </list>
    /// The session id is cached and lazily (re)minted; a request that comes back as
    /// permission-denied triggers exactly one refresh-and-retry.
    /// </summary>
    public sealed partial class FRITZBoxClient : IDisposable
    {
        private const string DeviceConfigService = "urn:dslforum-org:service:DeviceConfig:1";
        private const string HostsService = "urn:dslforum-org:service:Hosts:1";

        // The IGD (InternetGatewayDevice) surface — a separate, unauthenticated control tree that
        // reports the box' WAN uplink state. Readable with no credentials, so it works during
        // anonymous zero-conf discovery.
        private const string WANIPConnectionService = "urn:schemas-upnp-org:service:WANIPConnection:1";
        private const string WANIPConnectionControl = "igdupnp/control/WANIPConn1";

        private readonly Uri _soapBaseUrl;      // http(s)://host:49000/         (digest)
        private readonly Uri _restBaseUrl;      // …/api/v0/                     (AVM-SID)
        private readonly HttpClient _soap;
        private readonly HttpClient _rest;
        private readonly NetworkCredential? _credentials;
        private readonly ILogger _logger;

        /// <summary>Whether the box can be talked to authenticated. Anonymous clients still read the
        /// host table (MACs, incl. offline hosts) but cannot reach the privileged REST API.</summary>
        public bool CanAuthenticate => _credentials is not null;

        private readonly SemaphoreSlim _sidLock = new(1, 1);
        private string? _sid;

        /// <param name="host">The box' URI authority — an IP literal (IPv6 already bracketed) or a
        /// DNS name such as the box' own <c>fritz.box</c>.</param>
        /// <param name="credentials">TR-064 digest credentials, or null to talk to the box
        /// unauthenticated (enough for read-only host enumeration on some boxes).</param>
        public FRITZBoxClient(string host, NetworkCredential? credentials, bool tls, ILogger logger)
        {
            _logger = logger;
            _credentials = credentials;

            var scheme = tls ? "https" : "http";
            var soapPort = tls ? 49443 : 49000;

            _soapBaseUrl = new Uri($"{scheme}://{host}:{soapPort}/");
            _restBaseUrl = new Uri($"{scheme}://{host}/api/v0/");

            // TR-064 needs digest; HttpClient performs the 401 challenge/response itself, which
            // also sidesteps the box' quirk of answering a body-less probe with a 500.
            var soapHandler = new SocketsHttpHandler
            {
                Credentials = credentials,
                PreAuthenticate = false,
            };
            var restHandler = new SocketsHttpHandler();

            if (tls)
            {
                // The box presents a self-signed certificate; accept it on the local segment.
                soapHandler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
                restHandler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
            }

            _soap = new HttpClient(soapHandler) { Timeout = TimeSpan.FromSeconds(15) };
            _rest = new HttpClient(restHandler) { Timeout = TimeSpan.FromSeconds(15) };
        }

        #region REST

        /// <summary>GET a REST resource, deserializing into <typeparamref name="T"/>, refreshing
        /// the session once if the box reports the id as no longer valid.</summary>
        public Task<T> GetAsync<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken ct)
            => SendWithSidAsync(() => new HttpRequestMessage(HttpMethod.Get, new Uri(_restBaseUrl, path)), typeInfo, ct);

        /// <summary>PUT a JSON body to a REST resource (response body is ignored).</summary>
        public async Task PutAsync<TBody>(string path, TBody body, System.Text.Json.Serialization.Metadata.JsonTypeInfo<TBody> bodyInfo, CancellationToken ct)
        {
            HttpRequestMessage Factory()
            {
                var json = JsonSerializer.Serialize(body, bodyInfo);
                return new HttpRequestMessage(HttpMethod.Put, new Uri(_restBaseUrl, path))
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            using var response = await SendRawWithSidAsync(Factory, ct);
            await EnsureNotErrorAsync(response, ct);
        }

        public Task<EthernetPortList> GetEthPortsAsync(CancellationToken ct)
            => GetAsync("generic/eth_ports", FRITZBoxJsonContext.Default.EthernetPortList, ct);

        public Task PutEthPortAsync(string uid, EthernetPortUpdate update, CancellationToken ct)
            => PutAsync($"generic/eth_ports/eth/{uid}", update, FRITZBoxJsonContext.Default.EthernetPortUpdate, ct);

        #endregion

        #region Host enumeration (TR-064)

        /// <summary>Enumerates the box' known-host table — every leased host (with its MAC, even
        /// when offline) and every VPN peer.
        ///
        /// <para>With credentials this uses the authenticated <c>Hosts#X_AVM-DE_GetHostListPath</c>
        /// and reads the referenced <c>devicehostlist.lua</c> in one shot — the richest source, the
        /// only one that flags VPN peers (<see cref="FritzHost.IsVPN"/>). Without credentials (or if
        /// the box refuses them) it falls back to the anonymous per-entry enumeration
        /// (<c>GetHostNumberOfEntries</c> + <c>GetGenericHostEntry</c>), which every box answers
        /// unauthenticated and which still lists offline hosts with their MAC — enough to wake them,
        /// but with no VPN flag (a caller may infer a peer from a missing MAC).</para></summary>
        public async Task<IReadOnlyList<FritzHost>> GetHostsAsync(CancellationToken ct)
        {
            if (_credentials is not null)
            {
                try
                {
                    return await GetHostListAsync(ct);
                }
                catch (FRITZBoxAPIException ex)
                {
                    _logger.LogWarning(ex, "Authenticated FRITZ!Box host list failed; falling back to anonymous host enumeration.");
                }
            }

            return await EnumerateHostsAsync(ct);
        }

        /// <summary>Authenticated bulk host list via <c>X_AVM-DE_GetHostListPath</c>.</summary>
        private async Task<IReadOnlyList<FritzHost>> GetHostListAsync(CancellationToken ct)
        {
            var soap = await PostSoapAsync("upnp/control/hosts", HostsService, "X_AVM-DE_GetHostListPath", null, ct);

            var path = ValueRegex("NewX_AVM-DE_HostListPath").Match(soap) is { Success: true } m
                ? m.Groups[1].Value
                : throw new FRITZBoxAPIException("GetHostListPath returned no path.");

            var xml = await _soap.GetStringAsync(new Uri(_soapBaseUrl, path.TrimStart('/')), ct);

            return ParseHostList(xml);
        }

        /// <summary>Anonymous host enumeration: one <c>GetGenericHostEntry</c> SOAP call per host.
        /// Costlier than the bulk list (a call per entry) but needs no credentials.</summary>
        private async Task<IReadOnlyList<FritzHost>> EnumerateHostsAsync(CancellationToken ct)
        {
            var countXml = await PostSoapAsync("upnp/control/hosts", HostsService, "GetHostNumberOfEntries", null, ct);

            if (!int.TryParse(SoapValue(countXml, "NewHostNumberOfEntries"), out var count))
                throw new FRITZBoxAPIException("GetHostNumberOfEntries returned no count.");

            var hosts = new List<FritzHost>(count);

            for (var i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var xml = await PostSoapAsync("upnp/control/hosts", HostsService, "GetGenericHostEntry",
                    $"<NewIndex>{i}</NewIndex>", ct);

                var mac = TryParseMac(SoapValue(xml, "NewMACAddress"));
                var ip = IPAddress.TryParse(SoapValue(xml, "NewIPAddress"), out var parsed) ? parsed : null;
                var name = SoapValue(xml, "NewHostName");

                hosts.Add(new FritzHost
                {
                    Name = name ?? ip?.ToString() ?? mac?.ToString() ?? "?",
                    IP = ip,
                    MAC = mac,
                    IsActive = SoapValue(xml, "NewActive") == "1",
                    InterfaceType = SoapValue(xml, "NewInterfaceType") ?? "",
                    IsVPN = false, // the generic entry carries no VPN flag — a missing MAC is the only tell
                });
            }

            return hosts;
        }

        private static List<FritzHost> ParseHostList(string xml)
        {
            var hosts = new List<FritzHost>();

            foreach (var item in XDocument.Parse(xml).Descendants("Item"))
            {
                string? Text(string name) => item.Element(name)?.Value is { Length: > 0 } v ? v : null;

                hosts.Add(new FritzHost
                {
                    Name = Text("HostName") ?? Text("X_AVM-DE_FriendlyName") ?? "?",
                    IP = IPAddress.TryParse(Text("IPAddress"), out var ip) ? ip : null,
                    MAC = TryParseMac(Text("MACAddress")),
                    IsActive = Text("Active") == "1",
                    InterfaceType = Text("InterfaceType") ?? "",
                    IsVPN = Text("X_AVM-DE_VPN") == "1",
                });
            }

            return hosts;
        }

        private static PhysicalAddress? TryParseMac(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac))
                return null;

            // The box prints colon-separated MACs; PhysicalAddress.Parse wants no separators.
            return PhysicalAddress.TryParse(mac.Replace(":", "").Replace("-", ""), out var parsed) ? parsed : null;
        }

        private async Task<T> SendWithSidAsync<T>(Func<HttpRequestMessage> factory, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken ct)
        {
            using var response = await SendRawWithSidAsync(factory, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);

            ThrowIfApiError(response, payload);

            return JsonSerializer.Deserialize(payload, typeInfo)
                ?? throw new FRITZBoxAPIException($"Empty response from {factory().RequestUri}.");
        }

        /// <summary>Sends a REST request with the current session id, minting one first if
        /// needed and re-minting + retrying once on a permission-denied answer.</summary>
        private async Task<HttpResponseMessage> SendRawWithSidAsync(Func<HttpRequestMessage> factory, CancellationToken ct)
        {
            var sid = await GetSidAsync(forceRefresh: false, ct);
            var response = await SendOnceAsync(factory, sid, ct);

            if (IsPermissionDenied(response))
            {
                _logger.LogDebug("FRITZ!Box session rejected; re-minting and retrying once.");
                response.Dispose();

                sid = await GetSidAsync(forceRefresh: true, ct);
                response = await SendOnceAsync(factory, sid, ct);
            }

            return response;
        }

        private async Task<HttpResponseMessage> SendOnceAsync(Func<HttpRequestMessage> factory, string sid, CancellationToken ct)
        {
            var request = factory();
            request.Headers.TryAddWithoutValidation("Authorization", $"AVM-SID {sid}");
            return await _rest.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }

        private static bool IsPermissionDenied(HttpResponseMessage response)
            => response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || !response.IsSuccessStatusCode; // REST returns 200 for errors too — body is checked separately

        private static void ThrowIfApiError(HttpResponseMessage response, string payload)
        {
            // The REST API signals failures with { "errors": [ … ] } and often still HTTP 200.
            if (payload.Contains("\"errors\"", StringComparison.Ordinal))
            {
                var error = JsonSerializer.Deserialize(payload, FRITZBoxJsonContext.Default.APIErrorResponse);
                if (error?.Errors is { Count: > 0 } errors)
                {
                    var first = errors[0];
                    throw new FRITZBoxAPIException($"FRITZ!Box API error {first.Code}: {first.Message}");
                }
            }

            if (!response.IsSuccessStatusCode)
                throw new FRITZBoxAPIException($"FRITZ!Box API returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        private async Task EnsureNotErrorAsync(HttpResponseMessage response, CancellationToken ct)
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            ThrowIfApiError(response, payload);
        }

        #endregion

        #region WAN uplink (IGD, unauthenticated)

        /// <summary>The box' WAN connection status, from the unauthenticated IGD action
        /// <c>WANIPConnection#GetStatusInfo</c>. Typical values: <c>Connected</c> for a box with its
        /// own internet uplink (a mesh master), <c>Unconfigured</c> for a box fed through the mesh
        /// (a mesh slave/repeater). Null if the box reported no status.
        ///
        /// <para>Needs no credentials, so it classifies zero-conf discovered boxes too — it only
        /// depends on the box' "Transmit status information over UPnP" option being enabled (the
        /// default).</para></summary>
        public async Task<string?> GetWANConnectionStatusAsync(CancellationToken ct)
        {
            var xml = await PostSoapAsync(WANIPConnectionControl, WANIPConnectionService, "GetStatusInfo", null, ct);

            return SoapValue(xml, "NewConnectionStatus");
        }

        /// <summary>The box' public IPv4 as presented on its WAN uplink, from the unauthenticated IGD
        /// action <c>WANIPConnection#GetExternalIPAddress</c>. Null when the box has no WAN of its own
        /// (a mesh slave) or its uplink is currently down.</summary>
        public async Task<IPAddress?> GetExternalIPv4Async(CancellationToken ct)
        {
            var xml = await PostSoapAsync(WANIPConnectionControl, WANIPConnectionService, "GetExternalIPAddress", null, ct);

            // The box answers "0.0.0.0" when it has no public IPv4 — a DS-Lite / IPv6-only uplink, or
            // the brief window right after a reconnect before the lease arrives. Treat it as "none".
            return IPAddress.TryParse(SoapValue(xml, "NewExternalIPAddress"), out var ip)
                && ip.AddressFamily == AddressFamily.InterNetwork
                && !ip.Equals(IPAddress.Any)
                    ? ip
                    : null;
        }

        #endregion

        #region TR-064 session

        private async Task<string> GetSidAsync(bool forceRefresh, CancellationToken ct)
        {
            if (!forceRefresh && _sid is { } cached)
                return cached;

            await _sidLock.WaitAsync(ct);
            try
            {
                if (!forceRefresh && _sid is { } cachedInner)
                    return cachedInner;

                _sid = await CreateUrlSidAsync(ct);
                return _sid;
            }
            finally
            {
                _sidLock.Release();
            }
        }

        /// <summary>Calls <c>DeviceConfig#X_AVM-DE_CreateUrlSID</c> and returns the bare hex id
        /// (the box answers with <c>sid=&lt;hex&gt;</c>).</summary>
        private async Task<string> CreateUrlSidAsync(CancellationToken ct)
        {
            var body = await PostSoapAsync("upnp/control/deviceconfig", DeviceConfigService, "X_AVM-DE_CreateUrlSID", null, ct);

            return SidRegex().Match(body) is { Success: true } m
                ? m.Groups[1].Value
                : throw new FRITZBoxAPIException("CreateUrlSID returned no session id.");
        }

        /// <summary>Posts a TR-064 action (optionally with inner argument XML like
        /// <c>&lt;NewIndex&gt;0&lt;/NewIndex&gt;</c>) and returns the raw response body.</summary>
        private async Task<string> PostSoapAsync(string controlPath, string serviceType, string action, string? arguments, CancellationToken ct)
        {
            var envelope =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                $"<s:Body><u:{action} xmlns:u=\"{serviceType}\">{arguments}</u:{action}></s:Body></s:Envelope>";

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_soapBaseUrl, controlPath))
            {
                Content = new StringContent(envelope, Encoding.UTF8, "text/xml"),
            };
            request.Headers.TryAddWithoutValidation("SOAPACTION", $"\"{serviceType}#{action}\"");

            using var response = await _soap.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                throw new FRITZBoxAPIException(
                    $"{action} failed ({(int)response.StatusCode} {response.ReasonPhrase}) — check the FRITZ!Box credentials.");

            return body;
        }

        [GeneratedRegex("sid=([0-9a-fA-F]+)")]
        private static partial Regex SidRegex();

        private static Regex ValueRegex(string element) => new($"<{Regex.Escape(element)}>([^<]*)</{Regex.Escape(element)}>");

        /// <summary>The text of a SOAP response element, or null when absent/empty.</summary>
        private static string? SoapValue(string xml, string element)
            => ValueRegex(element).Match(xml) is { Success: true } m && m.Groups[1].Value is { Length: > 0 } v ? v : null;

        #endregion

        public void Dispose()
        {
            _soap.Dispose();
            _rest.Dispose();
            _sidLock.Dispose();
        }
    }

    public sealed class FRITZBoxAPIException(string message) : Exception(message);
}
