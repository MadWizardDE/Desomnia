using MadWizard.Desomnia.Configuration;
using System.Text.RegularExpressions;

namespace MadWizard.Desomnia.Process.Configuration
{
    // The pattern is mandatory: the only constructor takes it as XML text content
    // (or as a "pattern" attribute, which the binder maps to the constructor parameter).
    public class ProcessWatchInfo(string pattern)
    {
        public required string Name { get; set; }

        public Regex Pattern { get; } = new(pattern);

        public DelayedAction? OnStart { get; set; }
        public DelayedAction? OnStop { get; set; }

        public bool WatchChildren { get; set; } = false;

        public CPUThreshold MinCPU { get; set; }
    }
}
