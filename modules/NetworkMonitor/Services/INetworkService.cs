using PacketDotNet;

namespace MadWizard.Desomnia.Network
{
    public interface INetworkService
    {
        async Task Startup() => Resume();

        void Resume() { }

        void ProcessPacket(EthernetPacket packet) { }

        async Task BeforeSuspend() { }

        void Suspend() { }

        async Task Shutdown(NetworkShutdownReason reason) => Suspend();
    }

    public enum NetworkShutdownReason
    {
        ApplicationShutdown = 0,

        InterfaceDisconnected = 1
    }
}
