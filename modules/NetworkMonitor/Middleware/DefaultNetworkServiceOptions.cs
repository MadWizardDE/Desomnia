using Autofac;
using Autofac.Core.Resolving.Pipeline;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Configuration.Hosts;

namespace MadWizard.Desomnia.Network.Middleware
{
    public sealed class DefaultNetworkServiceOptions(NetworkMonitorConfig config) : IResolveMiddleware
    {
        public PipelinePhase Phase => PipelinePhase.ParameterSelection;

        public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
        {
            if (context.FirstParameterOfType<WatchedHostInfo>() is WatchedHostInfo configHost)
            {
                ApplyDefaultActions(configHost);
                ApplyDefaultAdvertiseOptions(configHost);
            }

            if (context.FirstParameterOfType<RemoteHostInfo>() is RemoteHostInfo configRemote)
            {
                ApplyDefaultKnockOptions(configRemote);
            }

            next(context);
        }

        private void ApplyDefaultActions(WatchedHostInfo config)
        {
            foreach (var service in config.Services)
            {
                service.OnDemand ??= config.OnServiceDemand;
            }
        }

        private void ApplyDefaultAdvertiseOptions(WatchedHostInfo configHost)
        {
            var options = configHost.MakeAdvertiseOptions(config);

            foreach (var service in configHost.Services)
            {
                service.Advertise               ??= options.Type;
                service.AdvertiseTimeout        ??= options.Timeout;
                service.AdvertiseHostTTL        ??= options.HostTTL;
                service.AdvertiseServiceTTL     ??= options.ServiceTTL;
            }
        }

        private void ApplyDefaultKnockOptions(RemoteHostInfo configHost)
        {
            foreach (var service in configHost.Services)
            {
                service.KnockMethod             ??= configHost.KnockMethod          ?? config.KnockMethod;
                service.KnockProtocol           ??= configHost.KnockProtocol        ?? config.KnockProtocol;
                service.KnockPort               ??= configHost.KnockPort            ?? config.KnockPort;

                service.KnockDelay              ??= configHost.KnockDelay           ?? config.KnockDelay;
                service.KnockRepeat             ??= configHost.KnockRepeat          ?? config.KnockRepeat;
                service.KnockTimeout            ??= configHost.KnockTimeout         ?? config.KnockTimeout;

                service.KnockSecret             ??= configHost.KnockSecret          ?? config.KnockSecret;
                service.KnockSecretAuth         ??= configHost.KnockSecretAuth      ?? config.KnockSecretAuth;
                service.KnockSecretAuthType     ??= configHost.KnockSecretAuthType  ?? config.KnockSecretAuthType;
                service.KnockSecretEncoding     ??= configHost.KnockSecretEncoding  ?? config.KnockSecretEncoding;
            }
        }
    }
}
