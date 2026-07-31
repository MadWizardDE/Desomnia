using MadWizard.Desomnia.Configuration.Binding;
using MadWizard.Desomnia.Display.Manager;
using MadWizard.Desomnia.Environments;
using NLog;

namespace MadWizard.Desomnia.Display.Environments
{
    /// <summary>
    /// Requires the laptop lid to be in a designated state (lid="open|closed"), answered by
    /// the built-in panel of the platform's <see cref="IDisplayManager"/>. On a machine
    /// without a lid — or where its state is unknowable — the condition is never satisfied:
    /// the attribute then deliberately selects nothing, reported once.
    /// </summary>
    public sealed class LidCondition : IEnvironmentCondition
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        readonly IDisplayManager _manager;

        readonly bool _requiredOpen;

        bool _warned;

        public LidCondition(IDisplayManager manager, string value)
        {
            _manager = manager;

            _requiredOpen = value.ToLowerInvariant() switch
            {
                "open" => true,
                "closed" => false,

                _ => throw new ConfigurationValueException($"Unknown lid state '{value}'; expected \"open\" or \"closed\"."),
            };
        }

        public bool IsSatisfied()
        {
            if (_manager.BuiltIn?.LidOpen is not bool open)
            {
                if (!_warned)
                {
                    _warned = true;

                    Logger.Warn("The lid state of this system could not be determined; treating lid conditions as not matched.");
                }

                return false;
            }

            return open == _requiredOpen;
        }

        EventHandler? _changed;

        // the built-in panel is the physical home of the lid; its typed transition event is
        // adapted here because the condition contract is the plain EventHandler shape
        public event EventHandler? Changed
        {
            add
            {
                if (_changed == null && _manager.BuiltIn is IDisplayBuiltIn builtIn)
                    builtIn.LidStateChanged += OnLidStateChanged;

                _changed += value;
            }
            remove
            {
                _changed -= value;

                if (_changed == null && _manager.BuiltIn is IDisplayBuiltIn builtIn)
                    builtIn.LidStateChanged -= OnLidStateChanged;
            }
        }

        void OnLidStateChanged(object? sender, bool open) => _changed?.Invoke(this, EventArgs.Empty);
    }
}
