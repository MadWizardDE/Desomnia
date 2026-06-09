using Autofac;
using MadWizard.Desomnia.Network.Filter;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Context
{
    public abstract class Context(ILifetimeScope parent) : IDisposable
    {
        protected ILogger Logger { get; set; } = null!;

        protected internal ILifetimeScope Scope
        {
            get;

            init
            {
                Logger = (ILogger)value.Resolve(typeof(ILogger<>).MakeGenericType(GetType()));

                parent.Disposer.AddInstanceForDisposal(field = value); // automatic child scope disposal
            }
        } = null!;

        protected void RegisterTrafficFilter(ContainerBuilder builder, params ITrafficType[] traffic)
        {
            builder.RegisterType<TrafficFilterRequest>()
                .WithParameter(TypedParameter.From(traffic))
                .AutoActivate();
        }

        public void Dispose()
        {
            Scope?.Dispose();
        }
    }
}
