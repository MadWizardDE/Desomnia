using Autofac;
using CommandLine;
using MadWizard.Desomnia;
using MadWizard.Desomnia.Daemon.Options;
using MadWizard.Desomnia.Logging;
using MadWizard.Desomnia.Network.Logging;
using Microsoft.Extensions.Hosting;
using NLog;

LogManager.Setup().SetupExtensions(ext => ext.RegisterLayoutRenderer<SleepTimeLayoutRenderer>("sleep-duration")); // FIXME
LogManager.Setup().SetupExtensions(ext => ext.RegisterLayoutRenderer<NetworkHostLayoutRenderer>()); // FIXME
LogManager.Setup().SetupExtensions(ext => ext.RegisterLayoutRenderer<NetworkLayoutRenderer>()); // FIXME
LogManager.Setup().SetupExtensions(ext => ext.RegisterLayoutRenderer<NetworkRealmLayoutRenderer>()); // FIXME

bool debug = false;
bool autoReload = false;
string? autoReloadPath = null;
Parser.Default.ParseArguments<CommandLineOptions>(args)
    .WithParsed(options =>
    {
        debug = options.Debug;
        autoReload = options.AutoReload;
        autoReloadPath = options.AutoReloadPath;
    })
    .WithNotParsed(errors =>
    {
        Environment.Exit(1);
    });

if (debug)
{
    await MadWizard.Desomnia.Test.Debugger.UntilAttached();
}

const string FHS_CONFIG_PATH = "/etc/desomnia"; // Filesystem Hierarchy Standard

string configPath = new ConfigDetector(FHS_CONFIG_PATH).Lookup();

try
{
    if (!Environment.IsPrivilegedProcess)
        throw new NotSupportedException("The application must be run with root privileges.");

    ConfigFileWatcher watcher;

    do
    {
        using (new SystemMutex("MadWizard.Desomnia", true)) using (watcher = new(autoReloadPath ?? configPath) { EnableRaisingEvents = autoReload })
        {
            var builder = new DesomniaDaemonBuilder(useFHS: configPath.StartsWith(FHS_CONFIG_PATH));

            builder.RegisterModule<MadWizard.Desomnia.CoreModule>();

            builder.RegisterModule<MadWizard.Desomnia.Daemon.PlatformModule>();

            builder.RegisterModule<MadWizard.Desomnia.Network.Module>();
            builder.RegisterModule<MadWizard.Desomnia.PowerRequest.Module>();
            builder.RegisterModule<MadWizard.Desomnia.Process.Module>();

#if !DESOMNIA_AOT
            builder.RegisterPluginModules();
#endif

            builder.LoadConfiguration(configPath);

            builder.Build().RunAsync(watcher.Token).Wait();
        }
    }
    while (watcher.HasChanged);

    return 0;
}
catch (Exception)
{
    throw;
}

class DesomniaDaemonBuilder(bool useFHS = false) : MadWizard.Desomnia.ApplicationBuilder
{
    const string FHS_LOG_PATH = "/var/log/desomnia";

    const string FHS_CORE_PLUGINS_PATH = "/usr/lib/desomnia/plugins";
    const string FHS_USER_PLUGINS_PATH = "/var/lib/desomnia/plugins";

    protected override string DefaultLogPath => useFHS ? FHS_LOG_PATH : base.DefaultLogPath;
    protected override string[] DefaultPluginsPaths => useFHS ? [FHS_CORE_PLUGINS_PATH, FHS_USER_PLUGINS_PATH] : base.DefaultPluginsPaths;
}
