using System.Reflection;

namespace MadWizard.Desomnia.Events
{
    public class ActionError(Event originalEvent, EventAction action, Exception exception)
    {
        public Event Event => originalEvent;
        public EventAction Action => action;
        public Exception? Exception => exception is TargetInvocationException target ? target.InnerException : exception;

        public EventMetaObject? Actor { get; init; }
    }
}
