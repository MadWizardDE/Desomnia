using PacketDotNet;

namespace MadWizard.Desomnia.Network.Neighborhood
{
    public abstract class NetworkService(string name)
    {
        public string Name => name;

        public abstract bool Accepts(Packet packet);
    }
}
