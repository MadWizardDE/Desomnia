namespace MadWizard.Desomnia.Environments
{
    /// <summary>
    /// A single requirement of an &lt;Environment&gt; block, created from one of its
    /// attributes. Implementations register in the persistent container (see
    /// <see cref="Module.LoadOnce"/>), keyed by the lowercase attribute name —
    /// <c>Named&lt;IEnvironmentCondition&gt;("power")</c> — and receive the attribute
    /// value as a <c>string</c> constructor parameter to parse and act upon. Their other
    /// dependencies are injected by the persistent container; the application container
    /// does not exist yet when they resolve. Base modules register their conditions
    /// with <c>PreserveExistingDefaults()</c>, so a platform module (which loads first)
    /// can take a condition over. Conditions evaluate directly against the current
    /// machine state.
    /// </summary>
    public interface IEnvironmentCondition
    {
        bool IsSatisfied();

        /// <summary>
        /// Raised when the underlying machine state may have changed. Implementations
        /// start/stop their change sources lazily in the add/remove accessors.
        /// </summary>
        event EventHandler? Changed;
    }
}
