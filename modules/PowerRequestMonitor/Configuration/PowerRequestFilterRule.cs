using System.Text.RegularExpressions;

namespace MadWizard.Desomnia.PowerRequest.Configuration
{
    // The pattern is mandatory: the only constructor takes it as XML text content
    // (or as a "pattern" attribute, which the binder maps to the constructor parameter).
    public class PowerRequestFilterRule(string pattern) // <- XML text content
    {
        public required string Name { get; set; }

        public Regex Pattern { get; } = new(pattern);

        public FilterRuleType Type { get; set; } = FilterRuleType.MustNot;
    }

    public enum FilterRuleType
    {
        MustNot = 0,
        Must
    }
}
