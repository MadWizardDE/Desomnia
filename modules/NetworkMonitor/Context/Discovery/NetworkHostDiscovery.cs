using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Context
{
    public partial class NetworkContext
    {
        internal async Task DiscoverHosts()
        {
            Logger.LogDebug("Discovering network hosts...");

            CreateLocalHost();

            foreach (var configHost in Config.Host)
            {
                CreateHost(new TypedParameter(typeof(NetworkHostInfo), configHost));
            }

            foreach (var configRange in Config.Ranges)
            {
                foreach (var configHostInRange in configRange.Host)
                {
                    CreateHost(new TypedParameter(typeof(NetworkHostInfo), configHostInRange));
                }
            }

            foreach (var configHost in Config.RemoteHost)
            {
                CreateHost(new TypedParameter(typeof(RemotePhysicalHostInfo), configHost));

                foreach (var configHostVirtual in configHost.VirtualHost)
                {
                    CreateHost(new TypedParameter(typeof(RemoteVirtualHostInfo), configHostVirtual), TypedParameter.From(configHost));
                }
            }
        }

        internal async Task DiscoverDynamicFilterHosts()
        {
            var contexts = new List<FilterContext>([this]).Concat(_hostContexts).Concat(_hostContexts.SelectMany(c => c));

            foreach (var ctx in contexts)
            {
                foreach (var host in ctx.FindMissingDynamicHosts(_hostContexts.Select(x => x.Host)).ToArray())
                {
                    var config = new NetworkHostInfo()
                    {
                        AutoDetect = Config.AutoDetect,

                        Name = host
                    };

                    CreateHost(new TypedParameter(typeof(NetworkHostInfo), config));
                }
            }
        }

        private void CreateLocalHost()
        {
            LocalHostInfo configHost;
            if (Config.LocalHost is not null)
            {
                if (Config.Service.Any() || Config.HTTPService.Any() || Config.VirtualHost.Any())
                    throw new Exception("You have to specify the configuration of local services and virtual hosts on the LocalHost node.");

                configHost = Config.LocalHost;
            }
            else
            {
                Config.HostFilterRule.Clear(); // don't register these filters twice

                configHost = Config;
            }

            CreateHost(new TypedParameter(typeof(LocalHostInfo), configHost));

            foreach (var configHostVirtual in configHost.VirtualHost)
            {
                if (VMManager[configHostVirtual.Name!] is IVirtualMachine vm)
                {
                    CreateHost(
                        new TypedParameter(typeof(LocalVirtualHostInfo), configHostVirtual),
                        new TypedParameter(typeof(IVirtualMachine), vm)
                    );
                }
            }
        }

        internal NetworkHostContext CreateHost(params Parameter[] parameters)
        {
            var context = Scope.Resolve<NetworkHostContext>(parameters);

            Network.AddHost(context.Host);

            if (context.Watch is NetworkHostWatch watch)
            {
                this.Monitor.StartTracking(watch);
            }

            context.Scope.CurrentScopeEnding += (sender, args) =>
            {
                Network.RemoveHost(context.Host);

                if (context.Watch is NetworkHostWatch watch)
                {
                    this.Monitor.StopTracking(watch);
                }

                _hostContexts.Remove(context);
            };

            _hostContexts.Add(context);

            return context;
        }

        public NetworkHostContext CreateDynamicHost(params Parameter[] parameters)
        {
            var context = CreateHost(parameters);

            Scope.Resolve<NetworkJanitor>().MakeHostEligibleForSweeping(context.Host);

            return context;
        }
    }
}
