using Autofac;
using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Context;
using MadWizard.Desomnia.Network.FRITZ.API;
using MadWizard.Desomnia.Network.FRITZ.Neighborhood;

namespace MadWizard.Desomnia.Network.FRITZ.Context
{
    /// <summary>
    /// The host scope of a FRITZ!Box router: registers the <see cref="FRITZBoxRouter"/> as the scope's
    /// <c>NetworkHost</c> (so it joins the <c>NetworkSegment</c> like any other router) together
    /// with the <see cref="FRITZBoxClient"/> it talks through. The client connects lazily and is
    /// disposed with the scope. Created via <c>NetworkContext.CreateRouter&lt;FRITZBoxRouterContext&gt;</c>,
    /// which also creates the (configured and box-discovered) VPN client hosts beforehand.
    /// </summary>
    public class FRITZBoxRouterContext : NetworkRouterContext
    {
        public FRITZBoxRouter Router => (FRITZBoxRouter)Host;

        public FRITZBoxRouterContext(ILifetimeScope parent, FRITZBoxClient client,
            Network.Configuration.NetworkMonitorConfig configNetwork, NetworkRouterInfo config)
                : base(parent, config.AutoDetect ?? configNetwork.AutoDetect)
        {
            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                RegisterRouter<FRITZBoxRouter>(builder, configNetwork, config);

                builder.RegisterInstance(client);
            });
        }
    }
}
