namespace MadWizard.Desomnia.Events
{
    /// <summary>
    /// Declares a URL action handler: the method handles every scheme-addressed action
    /// whose URL scheme matches <see cref="ActionHandlerAttribute.Name"/> (§6.4).
    /// Fully symmetric to [ActionHandler] — same declaration surface, same concurrency
    /// gate, same resolution walk (nearest node up the tree wins, root providers last).
    /// The method receives the <see cref="Uri"/> as a parameter and, if declared,
    /// objects from the event context.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class URLActionHandlerAttribute(string scheme) : ActionHandlerAttribute(scheme)
    {
    }
}
