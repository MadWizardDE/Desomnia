using MadWizard.Desomnia.Configuration.Binding;
using Microsoft.Extensions.Configuration.Xml;
using Microsoft.Extensions.Hosting;

namespace MadWizard.Desomnia
{
    public abstract class ConfigurableModule : Module
    {
        protected internal virtual void ConfigureConfigurationSource(ExtendedXmlConfigurationSource source) { }
    }

    public abstract class ConfigurableModule<T> : ConfigurableModule
    {
        public T Config { get; private set; } = default!;

        protected internal override void ConfigureConfigurationSource(ExtendedXmlConfigurationSource source)
        {
            base.ConfigureConfigurationSource(source);

            // Derive the names of nameless collection elements from the config type,
            // so the provider can synthesize deterministic name attributes for them.
            source.AddCollectionElementsOf(typeof(T));
        }

        protected internal override void Build(HostApplicationBuilder builder)
        {
            base.Build(builder);

            // Use the strict vendored binder: unknown keys stay tolerated (open format),
            // but invalid values abort startup instead of being silently swallowed.
            Config = StrictConfigurationBinder.Get<T>(builder.Configuration, opt => opt.BindNonPublicProperties = true)
                ?? throw new Exception($"Configuration binding for <{typeof(T).Name}> failed.");
        }
    }
}
