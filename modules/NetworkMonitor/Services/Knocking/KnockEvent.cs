using System.Net;

namespace MadWizard.Desomnia.Network.Knocking
{
    public readonly struct KnockEvent(IPAddress remote, IPPort? target = null)
    {
        public IPAddress    RemoteAddress   { get; init; } = remote;
        public IPPort?      TargetPort      { get; init; } = target;

        public DateTime     Time            { get; init; } = DateTime.Now;
    }
}
