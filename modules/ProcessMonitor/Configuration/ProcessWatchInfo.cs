using MadWizard.Desomnia.Configuration;
using System.Text.RegularExpressions;

namespace MadWizard.Desomnia.Process.Configuration
{
    public class ProcessWatchInfo
    {
        private readonly Regex? _pattern;

        public ProcessWatchInfo() { }

        public ProcessWatchInfo(string pattern) // <- XML text content
        {
            _pattern = new Regex(pattern);
        }

        public required string Name { get; set; }

        public Regex Pattern => _pattern ?? throw new ArgumentNullException("pattern");

        public DelayedAction? OnStart { get; set; }
        public DelayedAction? OnStop { get; set; }

        public bool WatchChildren { get; set; } = false;

        public CPUThreshold MinCPU { get; set; }
    }
}
