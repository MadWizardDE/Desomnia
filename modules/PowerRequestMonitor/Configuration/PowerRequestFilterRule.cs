using System.Text.RegularExpressions;

namespace MadWizard.Desomnia.PowerRequest.Configuration
{
    public class PowerRequestFilterRule
    {
        private readonly Regex? _pattern;

        public PowerRequestFilterRule() { }

        public PowerRequestFilterRule(string pattern) // <- XML text content
        {
            _pattern = new Regex(pattern);
        }

        public required string Name { get; set; }

        public Regex Pattern => _pattern ?? throw new ArgumentNullException("pattern");

        public FilterRuleType Type { get; set; } = FilterRuleType.MustNot;
    }

    public enum FilterRuleType
    {
        MustNot = 0,
        Must
    }
}
