using MadWizard.Desomnia.Configuration;
using MadWizard.Desomnia.Process.Configuration;

namespace MadWizard.Desomnia.Session.Configuration
{
    // Constructors are not inherited: the text-content constructor must be redeclared,
    // otherwise the binder cannot deliver the pattern to this derived type.
    public class SessionProcessWatchInfo(string pattern) : ProcessWatchInfo(pattern) // <- XML text content
    {
        public DelayedAction? OnSessionIdle { get; set; }
        public DelayedAction? OnSessionDemand { get; set; }
        public DelayedAction? OnSessionConsoleConnect { get; set; }
        public DelayedAction? OnSessionRemoteConnect { get; set; }
        public DelayedAction? OnSessionDisconnect { get; set; }
    }
}
