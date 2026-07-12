using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;

namespace MadWizard.Desomnia
{
    public class ApplicationBuilder
    {
        readonly HostApplicationBuilder _builder;

        readonly List<Module> _modules = [];

        protected virtual string DefaultLogLevelFormat => "${pad:padding=5:inner=${level:uppercase=true}}";
        protected virtual string DefaultLogFileLayout => "${longdate} " + DefaultLogLevelFormat + " ${logger:shortName=true} :: ${message} ${exception}";
        protected virtual string DefaultLogConsoleLayout => DefaultLogLevelFormat + " :: ${message} ${exception}";

        protected virtual string DefaultLogPath
        {
            get
            {
                if (Environment.GetEnvironmentVariable("DESOMNIA_LOG_DIR") is string logs)
                    return logs;

                return "${currentdir:dir=logs}";
            }
        }

        protected virtual string[] DefaultPluginsPaths
        {
            get
            {
                List<string> paths = [];

                if (Environment.GetEnvironmentVariable("DESOMNIA_PLUGINS_DIR") is string plugins)
                    paths.Add(plugins);
                if (Environment.GetEnvironmentVariable("DESOMNIA_CORE_PLUGINS_DIR") is string core)
                    paths.Add(core);
                if (Environment.GetEnvironmentVariable("DESOMNIA_USER_PLUGINS_DIR") is string user)
                    paths.Add(user);

                return paths.Count > 0 ? [.. paths] : ["plugins"];
            }
        }

        /**
         * Ideally the ContextRootPath should be left empty,
         * because the runtime will install file system watches
         * for every file below that path. On Linux this can
         * extend to the whole file system, if run as a systemd unit.
         */
        protected virtual HostApplicationBuilderSettings DefaultSettings => new()
        {
            DisableDefaults = true // don't set ContextRootPath to working directory
        };

        public ApplicationBuilder()
        {
            _builder = new HostApplicationBuilder(DefaultSettings);

            _builder.Services.AddSingleton<IHostLifetime, ConsoleLifetime>();

            _builder.ConfigureContainer(new AutofacServiceProviderFactory(), ConfigureContainer);

            ConfigureLogging();
        }

        protected virtual void ConfigureLogging(bool showTimestamp = false)
        {
            _builder.Logging.ClearProviders();

            _builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);

            if (LogManager.Configuration is LoggingConfiguration config)
            {
                if (!config.Variables.ContainsKey("logDir"))
                {
                    config.Variables["logDir"] = DefaultLogPath;
                }

                if (!config.Variables.ContainsKey("sharedLayout"))
                {
                    config.Variables["sharedLayout"] = DefaultLogFileLayout;
                }
            }
            else // Fallback if no config file has been found
            {
                config = new LoggingConfiguration();
            }

            LogManager.ConfigurationChanged += (sender, args) =>
            {
                if (args.ActivatedConfiguration is LoggingConfiguration configNew && !configNew.HasConsoleTarget())
                {
                    var target = new ConsoleTarget("console")
                    {
                        Layout = DefaultLogConsoleLayout
                    };

                    configNew.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, target, "MadWizard.Desomnia.*");

                    LogManager.Configuration = configNew;
                }
            };

            LogManager.Configuration = config;

            _builder.Logging.AddNLog();
        }

        protected void ConfigureContainer(ContainerBuilder container)
        {
#if DESOMNIA_AOT
            // AOT: supply strongly-typed metadata views without MakeGenericMethod, so consumers can keep
            // using Meta<A, B> unchanged. See AotMetadataViewSource.
            container.RegisterSource(new AotMetadataViewSource());
#endif
            foreach (var module in _modules)
            {
                container.RegisterModule(module);
            }
        }

        public void RegisterModule(Module module)
        {
            _modules.Add(module);
        }

        public void RegisterPluginModules()
        {
            foreach(var path in DefaultPluginsPaths)
            {
                this.RegisterPluginModules(path);
            }
        }

        public void LoadConfiguration(string path)
        {
            var source = new ExtendedXmlConfigurationSource(path, optional: false);

            foreach (var module in _modules.OfType<ConfigurableModule>())
            {
                module.ConfigureConfigurationSource(source);
            }

            _builder.Configuration.Sources.Add(source);

            //_builder.AddCommandLine(args);
        }

        public IHost Build()
        {
            foreach (var module in _modules)
            {
                module.Build(_builder);
            }

            return _builder.Build();
        }
    }
}