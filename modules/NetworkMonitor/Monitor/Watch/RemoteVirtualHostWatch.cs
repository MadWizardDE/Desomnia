using System.Net;

namespace MadWizard.Desomnia.Network.Watch
{
    public class RemoteVirtualHostWatch : RemoteHostWatch
    {
        public required RemoteHostWatch PhysicalWatch { private get; init; }

        protected internal override async Task WakeUp(IPAddress? ip = null)
        {
            if (!await Reachability.Test(PhysicalWatch, label: "physical host"))
            {
                await PhysicalWatch.WakeUp();
            }

            await base.WakeUp(ip);
        }

        protected override int SendMagicPacket(IPAddress? hint = null)
        {
            if (Host.PhysicalAddress is not null) // waking up virtual hosts can work without WoL
            {
                return base.SendMagicPacket(hint);
            }

            return 0;
        }
    }
}
