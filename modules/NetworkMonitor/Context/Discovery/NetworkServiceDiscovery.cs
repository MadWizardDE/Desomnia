using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Configuration.Services;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using MadWizard.Desomnia.Network.Reachability;
using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Context
{
    public partial class NetworkContext
    {
        internal async Task DiscoverServices()
        {
            Logger.LogDebug("Discovering services...");

            var reachability = Scope.Resolve<ReachabilityService>();

            foreach (var ctx in _hostContexts.Where(ctx => ctx.Auto.HasFlag(AutoDiscoveryType.Service)))
            {
                switch (ctx.Watch)
                {
                    /**
                     * If the user specified to detect services of remote hosts,
                     * the watched services are usually advertised to the Sleep Proxy,
                     * when the host suspends.
                     * 
                     * So in order to detect the services of an already sleeping host,
                     * we have to wake it once.
                     */
                    case RemoteHostWatch remote when !(await reachability.Test(remote)):
                        Logger.LogWarning("Remote host '{Host}' is not reachable." +
                            "Waking up now, in order to detect services.", ctx.Host.Name);

                        await remote.WakeUp(); break;
                }
            }
        }
    }

    public partial class NetworkHostContext
    {
        internal void CreateStaticServices(IEnumerable<ServiceInfo> services)
        {
            foreach (var info in services)
            {
                CreateService(info, new(ServiceFlags.Static));
            }
        }

        public NetworkHostServiceContext CreateService(ServiceInfo info, ServiceOptions options = default)
        {
            return CreateService<NetworkHostServiceContext>(
                new TypedParameter(typeof(ServiceInfo), info), 
                new TypedParameter(typeof(ServiceOptions), options));
        }

        public T CreateService<T>(params Parameter[] parameters) where T : NetworkHostServiceContext
        {
            var ctx = Scope.Resolve<T>(parameters);

            try
            {
                Host.AddService(ctx.Service, ctx.Options);

                Logger.LogHostServiceAdded(Host, ctx.Service);
            }
            catch (Exception) // service probably already exists
            {
                ctx.Dispose();

                throw;
            }

            Watch?.StartTracking(ctx.Watch);

            ctx.Scope.CurrentScopeEnding += (sender, args) =>
            {
                Host.RemoveService(ctx.Service);

                Logger.LogHostServiceRemoved(Host, ctx.Service);

                Watch?.StopTracking(ctx.Watch);

                _serviceContexts.Remove(ctx);
            };

            _serviceContexts.Add(ctx);

            return ctx;
        }
    }
}
