namespace MadWizard.Desomnia.Network.Filter.Rules
{
    public enum FilterRuleType : byte
    {
        May     = 0, // this rule matches, if other Must-Rules are present, but won't prevent a match if no Must-Rules are configured

        Must    = 1,
        MustNot = 2
    }
}
