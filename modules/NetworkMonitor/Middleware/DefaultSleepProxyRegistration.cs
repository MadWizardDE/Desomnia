using Autofac;
using Autofac.Core.Resolving.Pipeline;
using MadWizard.Desomnia.Network.Neighborhood.Services;
using MadWizard.Desomnia.Network.SleepProxy.Registration;
using MadWizard.Desomnia.Network.Watch;

namespace MadWizard.Desomnia.Network.Middleware
{
    public sealed class DefaultSleepProxyRegistration : IResolveMiddleware
    {
        public PipelinePhase Phase => PipelinePhase.ParameterSelection;

        public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
        {
            if (context.FirstParameterOfType<LocalHostWatch>() is LocalHostWatch watch)
            {
                context.ChangeParameters([
                    TypedParameter.From(watch.Host),
                    TypedParameter.From(watch.HandoffOptions),
                    TypedParameter.From(watch.SleepProxyRegistrationCycle)
                ]); next(context);

                if (context.Instance is SleepProxyRegistration reg)
                {
                    foreach (var watchService in watch)
                    {
                        if (watchService.Service is not TransportNetworkService service)
                            continue;

                        reg.Services.Add(new ProxyServiceInfo
                        {
                            Name = service.Name,
                            ServiceName = service.ServiceName,

                            Protocol = service.Port.Protocol,
                            Port = service.Port,

                            AdvertiseHostTTL = watch.AdvertiseOptions.HostTTL,
                            AdvertiseServiceTTL = watch.AdvertiseOptions.ServiceTTL,
                        });
                    }
                }
            }
        }
    }
}
