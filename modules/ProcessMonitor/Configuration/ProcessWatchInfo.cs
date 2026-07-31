using MadWizard.Desomnia.Configuration;
using System.Text.RegularExpressions;

namespace MadWizard.Desomnia.Processes.Configuration
{
    // The pattern is mandatory: the only constructor takes it as XML text content
    // (or as a "pattern" attribute, which the binder maps to the constructor parameter).
    public class ProcessWatchInfo(string pattern) // <- XML text content
    {
        public required string Name { get; set; }

        public bool IsFilePathPattern => pattern.Contains("\\\\") || pattern.Contains('/');

        public Regex Pattern { get; } = new(pattern);

        public DelayedActionInfo? OnIdle { get; set; }
        public DelayedActionInfo? OnDemand { get; set; }

        public DelayedActionInfo? OnStart { get; set; }
        public DelayedActionInfo? OnStop { get; set; }

        public bool WatchChildren { get; set; } = false;

        public CPUThreshold? MinCPU { get; set; }
    }
}
