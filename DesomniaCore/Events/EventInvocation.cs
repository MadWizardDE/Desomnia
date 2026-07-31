namespace MadWizard.Desomnia.Events
{
    public delegate Task EventInvocation(Event data);

    public delegate Task EventInvocation<T>(T data) where T : Event;

    /// <summary>WordPress-style filter chain: each subscriber receives the value and the
    /// event context and returns the (possibly modified) value. Folded in subscription
    /// order by the statically-generic Filter extensions — never by the pipeline.</summary>
    public delegate T EventFilter<T>(T data, Event context);

    /// <summary>Filter chain with strongly-typed event context.</summary>
    public delegate T EventFilter<T, TEvent>(T data, TEvent context) where TEvent : Event;
}
