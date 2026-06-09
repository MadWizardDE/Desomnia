using System.Net;

namespace MadWizard.Desomnia.Network.Knocking.Filter
{
    public class KnockSourceIPFilter : IKnockFilter
    {
        public bool ShouldFilter(IPEndPoint source, KnockEvent knock)
        {
            if (!source.Address.Equals(knock.RemoteAddress))
            {
                return true;
            }

            return false;
        }
    }
}
