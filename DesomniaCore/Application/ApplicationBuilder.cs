using Autofac;
using Autofac.Extensions.DependencyInjection;
using MadWizard.Desomnia.Environments;
using Microsoft.Extensions.Configuration.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using System.Runtime.CompilerServices;

namespace MadWizard.Desomnia
{
    /*
     * The builder is long-lived: modules are registered exactly once and survive application
     * restarts. Build() raises the persistent host — a Microsoft.Extensions host whose intrinsic
     * Autofac container is the machine-lifetime scope: it owns the process lifetime, the OS-facing
     * singletons (LoadOnce), the configuration authority and the rebuild loop. Each configuration
     * rebuild is a fresh inner host (BuildApplication) bridged to that same persistent scope.
     */
    public class ApplicationBuilder : IDisposable
    {
        const string CONFIG_FILE_NAME = "monitor.xml";
        const string NLOG_CONFIG_FILE_NAME = "NLog.config";

        // the process command line, captured at construction and reaching the hosts' Configuration
        readonly string[] _args;

        /// <summary>The configuration file the hosts build from. Constant for the process; the
        /// application loop rebuilds from this same path.</summary>
        internal protected string ConfigPath { get; private init; }

        readonly List<Module> _modules = [];

        // the persistent host and its Autofac container (the machine-lifetime scope). Built once by
        // Build(), disposed only when the whole application stops — NOT on a configuration rebuild,
        // so the services and the OS state they hold survive reconfiguration
        private IHost? _host;
        private ILifetimeScope? _persistent;

        /// <summary>Whether the loop watches the configuration file and rebuilds on a change.
        /// A process-bound convenience: defaulted from the command line, and a platform may set it
        /// directly (the Windows service always reloads).</summary>
        public bool AutoReload { get; set; }

        /// <summary>The path watched for auto-reload, when it differs from the configuration file.</summary>
        public string? AutoReloadPath { get; set; }

        /// <summary>Whether to wait for a debugger before starting; defaulted from the command line.</summary>
        public bool Debug { get; set; }

        /// <summary>The outer host's total stop budget: it must exceed an inner host's own
        /// shutdown plus the persistent container's disposal, or the SCM's managed wait gives
        /// up and reports the service stopped while teardown is still running.</summary>
        protected virtual TimeSpan OuterShutdownTimeout => TimeSpan.FromSeconds(60);

        #region Defaults
        protected virtual string DefaultLogLevelFormat => "${pad:padding=5:inner=${level:uppercase=true}}";
        protected virtual string DefaultLogFileLayout => "${longdate} " + DefaultLogLevelFormat + " ${logger:shortName=true} :: ${message} ${exception}";
        protected virtual string DefaultLogConsoleLayout => DefaultLogLevelFormat + " :: ${message} ${exception}";

        protected virtual string[] DefaultConfigPaths
        {
            get
            {
                List<string> paths = [];

                paths.Add(Directory.GetCurrentDirectory());

                paths.Add(Path.Combine(Directory.GetCurrentDirectory(), "config"));

                if (Environment.GetEnvironmentVariable("DESOMNIA_CONFIG_DIR") is string config)
                    paths.Add(config);

                return [.. paths];
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

        protected virtual string DefaultLogPath
        {
            get
            {
                if (Environment.GetEnvironmentVariable("DESOMNIA_LOG_DIR") is string logs)
                    return logs;

                return "${currentdir:dir=logs}";
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
        #endregion

        protected ApplicationBuilder(string[] args)
        {
            _args = args;

            // global process-bound options, parsed off the command line as a convenience; a
            // platform may still set them directly after construction (the Windows service always
            // enables auto-reload)
            AutoReload = HasFlag("--auto-reload") || HasFlag("-a");
            AutoReloadPath = FlagValue("--auto-reload-path") ?? FlagValue("-p");
            Debug = HasFlag("--debug");

            if (Debug)
                Test.Debugger.UntilAttached().Wait();

            ConfigPath = LookupConfigPath();

            bool HasFlag(string name) => Array.IndexOf(args, name) >= 0;
            string? FlagValue(string name)
            {
                var index = Array.IndexOf(args, name);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            }
        }

        /// <summary>Test seam: a builder bound to a fixed configuration file, with no command line.</summary>
        internal ApplicationBuilder(string configPath)
        {
            _args = [];

            ConfigPath = configPath;
        }

        private static string? LookupPath(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (Path.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return null;
        }

        protected virtual string LookupConfigPath()
        {
            return LookupPath(DefaultConfigPaths.Select(p => Path.Combine(p, CONFIG_FILE_NAME))) ?? CONFIG_FILE_NAME;
        }

        /// <summary>
        /// One-time global NLog setup. Per-host logging providers are wired
        /// per host in <see cref="Build"/> and <see cref="BuildApplication"/>.
        /// </summary>
        protected virtual void ConfigureLogging()
        {
            foreach (var module in _modules)
            {
                LogManager.Setup().SetupExtensions(module.ConfigureLogging);
            }

            if (LookupPath(DefaultConfigPaths.Select(p => Path.Combine(p, NLOG_CONFIG_FILE_NAME))) is string configNLogPath)
            {
                LogManager.Configuration = new XmlLoggingConfiguration(configNLogPath);
            }

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
        }

        /// <summary>
        /// Builds the persistent host: the process-lifetime Microsoft.Extensions host whose
        /// intrinsic Autofac container is the machine-lifetime scope. It owns the real
        /// <see cref="IHostLifetime"/> (the Windows service in service mode, the console lifetime
        /// otherwise), every module's <see cref="Module.LoadOnce(ContainerBuilder, string[])"/>
        /// singletons, the configuration authority and the rebuild loop. Running the returned host
        /// drives the loop in-process; only a genuine process stop or a fatal configuration brings
        /// it down.
        /// </summary>
        public IHost Build()
        {
            ConfigureLogging();

            var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = _args,           // reaches Configuration even under DisableDefaults
                DisableDefaults = true, // don't set ContentRootPath to the working directory
            });

            // the process's one logging stack: NLog's LogManager is global, so the provider that
            // fronts it belongs to the host that lives as long as the process. The inner hosts
            // share this factory instead of each bringing their own (see ConfigureApplication).
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            builder.Logging.AddNLog();

            builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = OuterShutdownTimeout);

            // the default process lifetime (DisableDefaults skips the framework's own): the console
            // lifetime handles Ctrl+C and SIGTERM, so systemd/launchd stop the daemons gracefully.
            // A platform (the Windows service) registers its own IHostLifetime in LoadOnce, later,
            // so it wins the single resolve — this one is then never constructed.
            builder.Services.AddSingleton<IHostLifetime, Microsoft.Extensions.Hosting.Internal.ConsoleLifetime>();

            builder.ConfigureContainer(new AutofacServiceProviderFactory(), ConfigurePersistent);

            _host = builder.Build();

            _persistent = _host.Services.GetAutofacRoot();

            PersistentServiceSource.ValidateLifetimes(_persistent);

            // resolve and activate the configuration authority now (compute the effective config,
            // start watching), so its watchers run before the loop builds the first inner host
            var environment = _persistent.Resolve<EnvironmentMonitor>();
            environment.EnableAutoReload(AutoReload, AutoReloadPath);
            environment.Activate(CollectionElements(), _persistent);

            return _host;
        }

        private void ConfigurePersistent(ContainerBuilder container)
        {
            // the builder itself, so the loop can rebuild inner hosts. As<ApplicationBuilder>, NOT
            // AsSelf: `this` is a platform subclass (e.g. DesomniaWindowsServiceBuilder), and AsSelf
            // would register only that runtime type, leaving the loop's ApplicationBuilder dependency
            // unresolvable. Externally owned: the host owns this instance, not its own container.
            container.RegisterInstance(this).As<ApplicationBuilder>().ExternallyOwned();

            // the configuration authority: created with the constant config path, resolved as a
            // managed component. Detect handles the <EnvironmentMonitor> (augmenting) mode, and
            // Passthrough the classic <SystemMonitor> — so the application always has one
            container.Register(_ => EnvironmentMonitor.Detect(ConfigPath) ?? EnvironmentMonitor.Passthrough(ConfigPath))
                .AsSelf()
                .SingleInstance();

            // the rebuild loop — the persistent host's one hosted service; the host starts it, and
            // it builds, runs and rebuilds the inner application hosts
            container.RegisterType<ApplicationLoopService>()
                .As<IHostedService>()
                .SingleInstance();

            foreach (var module in _modules)
            {
                module.LoadOnce(container, _args);
            }
        }

        /// <summary>Builds a fresh inner application host for one effective configuration, bridged
        /// to the persistent scope. Disposed (and rebuilt) by the loop on every reconfiguration.</summary>
        public IHost BuildApplication()
        {
            var builder = new HostApplicationBuilder(DefaultSettings);

            // the inner host must NOT own the process lifetime — that belongs to the persistent
            // host alone. A rebuild stops the inner host through the loop's linked token, never
            // through a console/SCM signal, so it gets the no-op lifetime.
            builder.Services.RemoveAll<IHostLifetime>();
            builder.Services.AddSingleton<IHostLifetime, PassiveLifetime>();

            builder.ConfigureContainer(new AutofacServiceProviderFactory(), ConfigureApplication);

            // deliberately no logging provider of its own: an NLog provider flushes the
            // process-global LogManager when it is disposed (NLogLoggerProvider.Dispose ->
            // LogFactory.Flush / FlushAsync), so every reload would flush the whole process's
            // logging — wasted work whose timeout surfaces as an unobserved TaskCanceledException.
            // The persistent factory is bridged in by ConfigureApplication instead.
            builder.Logging.ClearProviders();

            LoadConfiguration(builder, ConfigPath);

            foreach (var module in _modules)
            {
                module.Build(builder);
            }

            return builder.Build();
        }

        private void ConfigureApplication(ContainerBuilder container)
        {
            // bridge the persistent services in first, so registrations that gate on them
            // (e.g. the DisplayMonitor's OnlyIf IDisplayManager) see them at build time. The bridge's
            // export policy keeps the persistent host's framework services (hosting, options, the
            // loop) out of the inner container — it runs its own.
            container.RegisterSource(new PersistentServiceSource(_persistent!));

            // logging is the exception the policy cannot express, because it is process-global:
            // NLog's LogManager is, so the factory fronting it must be too. Registered after the
            // framework's own (populated from builder.Services) so this one answers, and
            // externally owned so a rebuild disposing this container never touches it.
            container.RegisterInstance(_persistent!.Resolve<ILoggerFactory>())
                .As<ILoggerFactory>()
                .ExternallyOwned();

            container.RegisterSource(new OrderedCollectionSource());

            if (!RuntimeFeature.IsDynamicCodeSupported)
            {
                container.RegisterSource(new AOTMetadataViewSource());
            }

            foreach (var module in _modules)
            {
                container.RegisterModule(module);
            }
        }

        /// <summary>Disposes the persistent host — and with it the Autofac container, restoring the
        /// OS state the machine-lifetime services hold. The application is stopping for good
        /// (a configuration rebuild keeps this builder, and the host, alive). Idempotent: the host
        /// also disposes itself when its run returns.</summary>
        public void Dispose() => _host?.Dispose();

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

        private void LoadConfiguration(HostApplicationBuilder builder, string path)
        {
            var source = new ExtendedXmlConfigurationSource(path, optional: false);

            foreach (var module in _modules.OfType<ConfigurableModule>())
            {
                module.ConfigureConfigurationSource(source);
            }

            // the authority injects the current effective configuration (augmenting mode) or leaves
            // the source to read the file (passthrough), and arms this build's reload token
            _persistent!.Resolve<EnvironmentMonitor>().InjectInto(source);

            builder.Configuration.Sources.Add(source);
        }

        /// <summary>The collection element names derived from every configurable module's config
        /// type — needed by the environment merger and stable across builds.</summary>
        private IReadOnlySet<string> CollectionElements()
        {
            var source = new ExtendedXmlConfigurationSource(ConfigPath, optional: true);

            foreach (var module in _modules.OfType<ConfigurableModule>())
            {
                module.ConfigureConfigurationSource(source);
            }

            return new HashSet<string>(source.CollectionElements, StringComparer.OrdinalIgnoreCase);
        }
    }
}
