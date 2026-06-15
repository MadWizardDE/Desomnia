using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Configuration.Services;
using MadWizard.Desomnia.Network.Neighborhood;
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
                    case RemoteHostWatch remote when remote.Host is not VirtualNetworkHost && !(await reachability.Test(remote)):
                        if (remote.Host.PhysicalAddress is not null)
                        {
                            Logger.LogInformation("Remote host '{Host}' is not reachable. " +
                                "Waking up now, in order to detect services.", ctx.Host.Name);

                            try
                            {
                                await remote.WakeUp();
                            }
                            catch (HostTimeoutException ex)
                            {
                                Logger.LogWarning("Remote host '{Host}' didn't wake up after {Timeout} s",
                                    ctx.Host.Name, Math.Ceiling(ex.Timeout.TotalSeconds));
                            }
                        }
                        else
                        {
                            Logger.LogWarning("Remote host '{Host}' is not reachable. " +
                                "Cannot wake up, in order to detect services, since it has no MAC address configured.", ctx.Host.Name);
                        }

                        break;
                }
            }
        }
    }

    public partial class NetworkHostContext
    {
        internal void CreateStaticWatchedServices(IEnumerable<WatchedServiceInfo> services)
        {
            foreach (var info in services)
            {
                CreateWatchedService(info, new(ServiceFlags.Static));
            }
        }

        public NetworkServiceContext CreateWatchedService(WatchedServiceInfo info, ServiceOptions options = default)
        {
            return CreateWatchedService<NetworkServiceContext>(
                new TypedParameter(typeof(WatchedServiceInfo), info), 
                new TypedParameter(typeof(ServiceOptions), options));
        }

        public T CreateWatchedService<T>(params Parameter[] parameters) where T : NetworkServiceContext
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
