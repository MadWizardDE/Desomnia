using MadWizard.Desomnia.Network.Knocking.Secrets;
using PacketDotNet;

namespace MadWizard.Desomnia.Network.Knocking
{
    public interface IKnockDetector
    {
        string Name => this.GetType().FullName!;

        IEnumerable<KnockEvent> Examine(IPPacket packet, SharedSecret secret);
    }
}
