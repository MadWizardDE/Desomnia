using Autofac;
using Autofac.Core.Resolving.Pipeline;

namespace MadWizard.Desomnia.Network.Middleware
{
    internal class DynamicNetworkMonitors<T> : IResolveMiddleware where T : class
    {
        public PipelinePhase Phase => PipelinePhase.Sharing;

        public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
        {
            next(context);

            var monitors = (IEnumerable<T>)context.Resolve<DynamicNetworkObserver>();

            context.Instance = ((IEnumerable<T>)context.Instance!).Union(monitors);
        }
    }
}
