using MadWizard.Desomnia.Power.Manager;

namespace MadWizard.Desomnia.PowerRequest.Configuration
{
    public class PowerManagerConfig
    {
        public InhibitionOperation  WatchOperation { get; set; }    = InhibitionOperation.Sleep;
        public InhibitionMode       WatchMode { get; set; }         = InhibitionMode.Block | InhibitionMode.BlockWeak;
    }
}
