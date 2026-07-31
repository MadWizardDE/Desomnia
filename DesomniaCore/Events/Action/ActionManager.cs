using Autofac;
using MadWizard.Desomnia.Configuration;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Events
{
    public class ActionManager : IStartable, IEventSystemRoot
    {
        public required ILogger<ActionManager> Logger { protected get; init; }

        private readonly List<ActionProvider> _providers = [];

        /// <summary>The root providers (§5) — collected from `.As&lt;ActionProvider&gt;()`
        /// registrations in registration order. Their [ActionHandler] AND
        /// [URLActionHandler] declarations form the catch-all of the resolution walk.</summary>
        public required IEnumerable<ActionProvider> InjectableProviders { private get; init; }

        void IStartable.Start()
        {
            foreach (var provider in InjectableProviders)
            {
                _providers.Add(provider);
            }
        }

        public async Task<bool> TryHandleEventAction(Event eventObj, EventAction action)
        {
            foreach (var provider in _providers)
            {
                try
                {
                    // SELF step only — an attached root provider walking back into the
                    // root fallback here would recurse into this very method
                    if (await provider.DispatchSelfAsync(eventObj, action))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    HandleActionError(new ActionError(eventObj, action, ex) { Actor = provider });

                    return true;
                }
            }

            return false;
        }

        public bool HandleActionError(ActionError error)
        {
            string postfix = ":";
            if (error.Actor != null && error.Event.Source != error.Actor)
                postfix = $" @ {error.Actor.GetType().Name}:";

            Logger.LogError(error.Exception, $"{error.Event} -> {error.Action}" + postfix);

            return true;
        }
    }
}
