using Autofac;
using MadWizard.Desomnia.Network.Filter;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Context
{
    public abstract class Context : IDisposable
    {
        protected ILogger Logger { get; set; } = null!;

        protected internal ILifetimeScope Scope
        {
            get;

            init
            {
                field = value;

                Logger = (ILogger) value.Resolve(typeof(ILogger<>).MakeGenericType(GetType()));
            }
        } = null!;

        protected void RegisterTrafficFilter(ContainerBuilder builder, params ITrafficType[] traffic)
        {
            builder.RegisterType<TrafficFilterRequest>()
                .WithParameter(TypedParameter.From(traffic))
                .AutoActivate();
        }

        public virtual void Dispose()
        {
            Scope?.Dispose();
        }
    }
}
