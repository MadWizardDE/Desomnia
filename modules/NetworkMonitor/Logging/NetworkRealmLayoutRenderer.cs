using NLog;
using NLog.LayoutRenderers;
using System.Text;

namespace MadWizard.Desomnia.Network.Logging
{
    [LayoutRenderer("realm")]
    public class NetworkRealmLayoutRenderer : LayoutRenderer
    {
        protected override void Append(StringBuilder sb, LogEventInfo logEvent)
        {
            if (ScopeContext.TryGetProperty("Realm", out var property) && property is string realm)
            {
                sb.Append("_");
                sb.Append(realm);
            }
        }
    }
}
