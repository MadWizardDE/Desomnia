using Autofac;
using Autofac.Core.Resolving.Pipeline;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.SleepProxy.Registration;
using MadWizard.Desomnia.Network.Watch;
using System.Net;

namespace MadWizard.Desomnia.NetworkSession
{
    public sealed class SMBSleepProxyRegistration : IResolveMiddleware
    {
        public PipelinePhase Phase => PipelinePhase.ParameterSelection;

        public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
        {
            next(context);

            if (context.FirstParameterOfType<LocalHostWatch>() is LocalHostWatch watch && watch.Host is LocalHost)
            {
                if (context.Instance is SleepProxyRegistration reg)
                    reg.Services.Add(new ProxyServiceInfo(watch.AdvertiseOptions)
                    {
                        Name = "SMB",
                        ServiceName = "smb",

                        Protocol = IPProtocol.TCP,
                        Port = 445,
                    });
            }
        }
    }
}
