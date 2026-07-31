using PacketDotNet;

namespace MadWizard.Desomnia.Network
{
    public interface INetworkService
    {
        async Task Startup() => Resume();

        async Task AfterStartup() { }

        void Resume() { }

        void ProcessPacket(EthernetPacket packet) { }

        async Task BeforeSuspend() { }

        void Suspend() { }

        async Task Shutdown(NetworkShutdownReason reason) => Suspend();
    }

    public enum NetworkShutdownReason
    {
        ApplicationShutdown = 0,

        /// <summary>
        /// The interface is still operational (unlike <see cref="InterfaceDisconnected"/>),
        /// but about to be shut down.
        ///
        /// This may happen because a &lt;NetworkInterfaceBlock&gt; designates it — of an
        /// environment, or of another monitored network taking priority. The monitor is
        /// stopped BEFORE the block disables the interface, while the adapter is still up.
        /// </summary>
        InterfaceShutdown = 1,

        InterfaceDisconnected = 2,
    }
}
