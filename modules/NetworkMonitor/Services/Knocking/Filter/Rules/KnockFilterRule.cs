using MadWizard.Desomnia.Network.Filter.Rules;
using System.Net;

namespace MadWizard.Desomnia.Network.Knocking.Filter.Rules
{
    public abstract class KnockFilterRule : FilterRule
    {
        public abstract bool Matches(IPEndPoint source, KnockEvent knock);
    }
}
