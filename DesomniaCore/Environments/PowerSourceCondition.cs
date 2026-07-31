using MadWizard.Desomnia.Configuration.Binding;
using MadWizard.Desomnia.Power.Source;
using NLog;

namespace MadWizard.Desomnia.Environments
{
    /// <summary>
    /// Requires the system to run on a designated power source (power="ac|battery").
    /// The platform hosts supply this condition from their PlatformModule, backed by
    /// their <see cref="IPowerSource"/> implementation.
    /// </summary>
    public sealed class PowerSourceCondition : IEnvironmentCondition
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        readonly PowerSource _required;

        bool _warned;

        public required IPowerSource Probe { private get; init; }

        public PowerSourceCondition(string value)
        {
            _required = value.ToLowerInvariant() switch
            {
                "ac" => PowerSource.AC,
                "battery" => PowerSource.Battery,

                _ => throw new ConfigurationValueException($"Unknown power source '{value}'; expected \"ac\" or \"battery\"."),
            };
        }

        public bool IsSatisfied()
        {
            var current = Probe.Source;

            if (current == PowerSource.Unknown && !_warned)
            {
                _warned = true;

                Logger.Warn("The power source of this system could not be determined; treating power conditions as not matched.");
            }

            return current == _required;
        }

        public event EventHandler? Changed
        {
            add => Probe.PowerSourceChanged += value;
            remove => Probe.PowerSourceChanged -= value;
        }
    }
}
