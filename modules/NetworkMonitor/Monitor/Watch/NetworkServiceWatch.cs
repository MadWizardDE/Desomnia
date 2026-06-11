using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Neighborhood;
using PacketDotNet;

namespace MadWizard.Desomnia.Network.Watch
{
    public class NetworkServiceWatch(NetworkService service) : NetworkWatch<Resource>
    {
        public virtual bool IsHidden { get; set; } = false;

        public NetworkService Service => service;

        public AdvertiseOptions     AdvertiseOptions    { get; init; }
        public KnockOptions?        KnockOptions        { get; init; }

        public bool CanTriggerDemand(EthernetPacket trigger)
        {
            return IsIdle && HasEventHandlers(nameof(Demand)) && Service.Accepts(trigger);
        }

        protected internal override void ReportNetworkTraffic(EthernetPacket packet)
        {
            if (Service.Accepts(packet))
            {
                base.ReportNetworkTraffic(packet);
            } 
        }

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            if (HadThresholdTraffic(interval, out long bytes))
            {
                yield return new NetworkServiceUsage(Service, bytes);
            }
        }
    }
}
