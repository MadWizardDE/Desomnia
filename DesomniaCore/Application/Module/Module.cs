using Autofac;
using Microsoft.Extensions.Hosting;
using NLog.Config;

namespace MadWizard.Desomnia
{
    public abstract class Module : Autofac.Module
    {
        protected internal virtual void ConfigureLogging(ISetupExtensionsBuilder builder) { }

        protected internal virtual void Build(HostApplicationBuilder builder) { }

        /// <summary>
        /// Registers services whose lifetime is the machine, not any one effective
        /// configuration. Called exactly once per module, when the persistent container is
        /// built at boot; the resulting persistent container is shared by every
        /// configuration rebuild and disposed only when the whole application stops.
        /// Because this runs once, no configuration is available here — persistent
        /// services must work without one. Stateful services holding OS resources are
        /// registered as singletons; stateless helpers (matchers, environment conditions)
        /// may stay transient, as long as they are not disposable — disposed-per-resolve
        /// does not exist in a machine-lifetime container (checked at build where the
        /// registration reveals its type; delegate-created instances are checked when a
        /// bridged resolve surfaces one). Per-scope lifetimes are rejected, and
        /// open-generic registrations are not bridged — register closed types.
        /// Every registration is bridged into each application container by the
        /// <see cref="PersistentServiceSource"/>, so the application resolves and uses
        /// the services but never disposes them.
        /// </summary>
        protected internal virtual void LoadOnce(ContainerBuilder builder) { }

        /// <summary>
        /// The command-line-aware overload of <see cref="LoadOnce(ContainerBuilder)"/>, so a
        /// module can parse its own process-bound options (each parses the ones it needs,
        /// ignoring the rest) and register accordingly. The default forwards to the
        /// argument-less overload; a module that needs the command line overrides this one.
        /// </summary>
        protected internal virtual void LoadOnce(ContainerBuilder builder, string[] args) => LoadOnce(builder);
    }
}
