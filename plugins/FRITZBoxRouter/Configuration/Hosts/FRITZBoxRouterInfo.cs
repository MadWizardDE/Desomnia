using MadWizard.Desomnia.Network.Configuration.Hosts;
using System.Net;

namespace MadWizard.Desomnia.Network.FRITZ.Configuration
{
    /// <summary>
    /// A single &lt;FRITZBoxRouter&gt; element, e.g.
    /// <code>&lt;FRITZBoxRouter username="Kevin" password="…" /&gt;</code>
    ///
    /// <para>A FRITZ!Box <em>is</em> a router, so this extends <see cref="NetworkRouterInfo"/> and
    /// plugs straight into the NetworkMonitor router pipeline: it inherits <c>Name</c>, the
    /// <c>IPv4</c>/<c>IPv6</c> address, <c>MAC</c>, the <c>&lt;VPNClient&gt;</c> list and the
    /// <c>allowWake…</c> router options. On top of that it adds the credentials and transport
    /// needed to reach the box' APIs (host/VPN enumeration, LAN port control).</para>
    ///
    /// <para>Neither an address nor a name is required: <see cref="Name"/> defaults to
    /// <c>fritz.box</c>, which every FRITZ!Box answers for, so the box is reachable and its
    /// addresses are resolved by ordinary host discovery — exactly like any other host.</para>
    ///
    /// <para><see cref="NetworkHostInfo.Name"/> is also how <c>fritz://&lt;name&gt;/…</c> URL
    /// actions address the box; it is independent of the network the element is nested under.</para>
    ///
    /// <para>Credentials are required for any privileged operation: changing a port speed goes
    /// through the box' REST API (<c>/api/v0/…</c>), whose session id is minted over TR-064
    /// (<c>X_AVM-DE_CreateUrlSID</c>), and that call is HTTP-digest authenticated.</para>
    /// </summary>
    public class FRITZBoxRouterInfo : NetworkRouterInfo
    {
        private string? Username    { get; set; }
        private string? Password    { get; set; }

        /// <summary>Talk to the box over TLS (TR-064 :49443 / REST :443) instead of plain HTTP.
        /// Off by default: on the local segment the digest handshake never exposes the password
        /// and this avoids the box' self-signed-certificate handling.</summary>
        public bool TLS { get; set; } = false;

        public FRITZBoxRouterInfo()
        {
            Name = "fritz.box";
        }

        public NetworkCredential? Credentials
        {
            get
            {
                if (Username != null && Password != null)
                {
                    return new(Username, Password);
                }

                return null;
            }
        }
    }
}
