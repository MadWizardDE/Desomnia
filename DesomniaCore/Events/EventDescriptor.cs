namespace MadWizard.Desomnia.Events
{
    /// <summary>
    /// Immutable, first-class declaration of an event type. CLR "event EventInvocation"
    /// members are wrapped into descriptors at construction (annotations applied);
    /// plugins declare additional events at runtime via <see cref="IEventSystem.AddDynamicEvent"/>.
    /// </summary>
    public sealed record EventDescriptor
    {
        /// <summary>Events forming a SYMMETRIC cancel pair with this one: triggering either
        /// side aborts the other side's pending (scheduled/throttled) actions.</summary>
        public IReadOnlyList<string> Opposites { get; init; } = [];

        /// <summary>Directed relation: events whose pending actions are aborted whenever
        /// THIS event triggers (no reverse effect).</summary>
        public IReadOnlyList<string> Cancels { get; init; } = [];

        /// <summary>Reserved: event-level concurrency knob (see EventOptionsAttribute).</summary>
        public bool Concurrent { get; init; } = false;

        /// <summary>Handlers and bound actions are awaited concurrently (Task.WhenAll)
        /// instead of sequentially in add order.</summary>
        public bool Parallel { get; init; } = false;
    }

    [AttributeUsage(AttributeTargets.Event, AllowMultiple = true)]
    public sealed class EventOppositeAttribute(params string[] opposites) : Attribute
    {
        public string[] Opposites => opposites;
    }

    [AttributeUsage(AttributeTargets.Event, AllowMultiple = true)]
    public sealed class EventCancelsAttribute(params string[] cancels) : Attribute
    {
        public string[] Cancels => cancels;
    }

    [AttributeUsage(AttributeTargets.Event, AllowMultiple = false)]
    public sealed class EventOptionsAttribute : Attribute
    {
        public bool Concurrent { get; set; }

        public bool Parallel { get; set; }
    }
}
