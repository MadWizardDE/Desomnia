
using Autofac;

namespace MadWizard.Desomnia.Ressource
{
    public abstract class DynamicResourceMonitor<T> : ResourceMonitor<T>, IStartable, IDisposable where T : IInspectable
    {
        public required ILifetimeScope Scope { private get; init; }

        public virtual void Start()
        {
            foreach (var monitor in Scope.Resolve<IEnumerable<T>>())
            {
                this.StartTracking(monitor);
            }
        }

        // the sync-on-inspect re-resolution that used to live here was the brittle
        // seam (spec §7.2): dynamically born resources dangled until the first
        // inspection — and forever with timeout="". Nested-scope actors are handed
        // over explicitly by their scope owner now (NetworkInspectionBridge).

        public override void Dispose()
        {
            foreach (var monitor in this)
            {
                this.StopTracking(monitor);
            }

            base.Dispose();
        }

    }
}
