using System.Net;

namespace MadWizard.Desomnia.Network.Knocking.Filter
{
    public class KnockTimeFilter(TimeSpan timeout) : IKnockFilter
    {
        public bool ShouldFilter(IPEndPoint source, KnockEvent knock)
        {
            var runtime = DateTime.Now - knock.Time;

            if (runtime < TimeSpan.Zero || runtime > timeout)
            {
                return true;
            }

            return false;
        }
    }
}
