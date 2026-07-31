using CommandLine;

namespace MadWizard.Desomnia.Daemon.Options
{
    internal class CommandLineOptions
    {
        [Option("debug", Required = false, Default = false, HelpText = "Wait for a debugger to attach before starting.")]
        public bool Debug { get; set; }

        [Option('a', "auto-reload", Required = false, Default = false, HelpText = "Enable automatic reloading after config file changed.")]
        public bool AutoReload { get; set; }

        [Option('p', "auto-reload-path", Required = false, HelpText = "Enable automatic reloading after config file changed (in a different directory).")]
        public string? AutoReloadPath { get; set; }

        [Option("no-dbus", Required = false, Default = false, HelpText = "Do not use D-Bus/logind; fall back to the sysfs power manager.")]
        public bool NoDBus { get; set; }
    }
}
