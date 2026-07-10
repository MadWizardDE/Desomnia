using Autofac;
using Autofac.Core.Resolving.Pipeline;
using MadWizard.Desomnia.Network.Datagram;

namespace MadWizard.Desomnia.Network.Middleware
{
    /// <summary>
    /// Realizes a <see cref="DatagramService.SocketMetadata"/> declaration: when a
    /// <see cref="DatagramService"/> registered with the metadata is constructed, the instance is
    /// linked to the application-wide <see cref="UDPSocketService"/> -- allocating the OS socket
    /// alongside the packet capturing -- and the link handle is put into the service's lifetime
    /// scope, so disposing the service unlinks it again and the socket closes with its last user.
    /// A service without the metadata (like the multicast DNS service) stays capture-only.
    /// </summary>
    public sealed class DatagramSocketLink : IResolveMiddleware
    {
        public PipelinePhase Phase => PipelinePhase.Activation;

        public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
        {
            next(context);

            if (context.Instance is not DatagramService service)
                return;

            if (!context.Registration.Metadata.TryGetValue(nameof(DatagramService.SocketMetadata.Port), out var value) || value is not ushort port)
                return;

            bool shared = context.Registration.Metadata.TryGetValue(nameof(DatagramService.SocketMetadata.Shared), out var flag) && flag is true;

            var link = context.Resolve<UDPSocketService>().Link(service, port, shared);

            context.ActivationScope.Disposer.AddInstanceForDisposal(link);
        }
    }
}
