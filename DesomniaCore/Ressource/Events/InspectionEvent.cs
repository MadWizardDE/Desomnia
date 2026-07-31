using MadWizard.Desomnia.Events;

namespace MadWizard.Desomnia
{
    public class InspectionEvent(string type) : Event(type)
    {
        public required IEnumerable<UsageToken> Tokens { get; init; }

        public required TimeSpan Duration { get; init; }
    }
}
