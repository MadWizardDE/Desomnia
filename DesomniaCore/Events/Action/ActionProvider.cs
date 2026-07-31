namespace MadWizard.Desomnia.Events
{
    /// <summary>
    /// Base of the ROOT action providers (§5): global objects whose [ActionHandler]s
    /// form the catch-all of the resolution walk — consulted once, in registration
    /// order, after a dispatch walked its whole tree unhandled. Registered
    /// <c>.As&lt;ActionProvider&gt;()</c> and collected by the <see cref="ActionManager"/>.
    /// A dedicated abstraction (not a bare EventMetaObject registration), so global
    /// providers stay descriptively separate from regular event objects.
    /// </summary>
    public abstract class ActionProvider : EventMetaObject { }
}
