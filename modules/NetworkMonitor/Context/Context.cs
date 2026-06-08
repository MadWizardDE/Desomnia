using Autofac;
using MadWizard.Desomnia.Network.Filter;

namespace MadWizard.Desomnia.Network.Context
{
    public abstract class Context(ILifetimeScope parent) : IDisposable
    {
        protected internal ILifetimeScope Scope
        {
            get;

            init
            {
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
