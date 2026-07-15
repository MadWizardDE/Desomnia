using MadWizard.Desomnia.Configuration.Binding;
using Microsoft.Extensions.Configuration.Xml;
using Microsoft.Extensions.Hosting;

namespace MadWizard.Desomnia
{
    public abstract class ConfigurableModule : Module
    {
        /// <summary>The module's configuration type, used to derive collection element names.</summary>
        protected internal virtual Type? ConfigType => null;

        protected internal virtual void ConfigureConfigurationSource(ExtendedXmlConfigurationSource source) { }
    }

    public abstract class ConfigurableModule<T> : ConfigurableModule
    {
        public T Config { get; private set; } = default!;

        protected internal override Type ConfigType => typeof(T);

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
