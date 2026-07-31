using System.Reflection;

namespace MadWizard.Desomnia.Events
{
    /// <summary>Per-event meta object shared by action events and filter events.</summary>
    public abstract class EventTypeBase(EventMetaObject owner, string name, EventDescriptor descriptor)
    {
        private EventMetaObject _owner = owner;

        public string Name => name;

        public EventDescriptor Descriptor { get; } = descriptor;

        /// <summary>The declaring object — or the detached orphan host after
        /// <see cref="Orphan"/> (§7.4): never null, never the dead object.</summary>
        public EventMetaObject Owner => Volatile.Read(ref _owner);

        public bool IsOrphaned { get; private set; }

        /// <summary>Detaches this event from its (dying) owner at the shutdown seam
        /// (§7.4): live pendings are cancelled, subscribers are dropped, the owner's
        /// [EventContext] values are snapshotted, and subsequent triggers replay the
        /// bound actions at the ROOT with the snapshot as context. Only runtime-declared
        /// events can be orphaned. Idempotent.</summary>
        public void Orphan() => _owner.OrphanEventType(this);

        internal void ReOwn(EventMetaObject host)
        {
            Volatile.Write(ref _owner, host);                 // paired with lock-free readers
            IsOrphaned = true;                                // (bindings, trigger routing)
        }

        private string[] _effectiveCancels = [];

        /// <summary>Names whose pending actions this event aborts when triggered — the
        /// merged view of Opposites (both directions) and Cancels (directed). Replaced
        /// atomically on recompute (declare/orphan), never mutated in place: readers on
        /// other locks/threads always see a coherent snapshot.</summary>
        internal string[] EffectiveCancels
        {
            get => Volatile.Read(ref _effectiveCancels);
            set => Volatile.Write(ref _effectiveCancels, value);
        }
    }

    /// <summary>
    /// The rich per-event handle for action events (EventInvocation delegates): typed
    /// trigger path, handler subscription, action binding with engine-managed
    /// scheduling, and cancellation.
    /// </summary>
    public abstract class EventType(EventMetaObject owner, string name, EventDescriptor descriptor)
        : EventTypeBase(owner, name, descriptor)
    {
        private readonly List<ActionBinding> _bindings = [];

        private EventInvocation? _anchor;

        /// <summary>Total trigger observations, counted by the anchor — advances even
        /// when no subscriber and no action is attached.</summary>
        public long TriggerCount => Interlocked.Read(ref _triggerCount);
        private long _triggerCount;

        // ── trigger ──────────────────────────────────────────────────────────────

        /// <summary>Synchronous trigger — blocks like the legacy TriggerEvent; delayed
        /// actions arm and return, immediate ones complete before this returns.</summary>
        public void TriggerEvent(Event? @event = null) => TriggerEventAsync(@event).Wait();

        public Task TriggerEventAsync(Event? @event = null)
        {
            @event ??= new Event(Name);
            @event.Type = Name;                              // today's silent-rename behavior

            return Owner.RouteTriggerAsync(@event);          // honors legacy veto overrides
        }

        // ── actions ──────────────────────────────────────────────────────────────

        /// <summary>Binds an action to this event. Config ActionInfos arrive through
        /// their implicit conversion (the engine border, §6.2) — null means an unset or
        /// blank XML attribute and is a no-op, so config wiring stays a one-liner.</summary>
        public ActionBinding? AddAction(EventAction? action)
        {
            if (action == null)
                return null;

            var binding = new ActionBinding(this, action);

            AddHandler(binding.Invocation);                  // occupies its ADD-ORDER position

            lock (_bindings)
                _bindings.Add(binding);

            return binding;
        }

        public bool RemoveAction(ActionBinding binding)
        {
            lock (_bindings)
            {
                if (!_bindings.Remove(binding))
                    return false;
            }

            binding.Retire();                    // NOT just Cancel: an in-flight trigger may still hold
            RemoveHandler(binding.Invocation);   // the wrapper in its entry snapshot — a retired binding
                                                 // refuses to re-arm, so no unreachable pending can form
            return true;
        }

        public IReadOnlyList<ActionBinding> Actions { get { lock (_bindings) return [.. _bindings]; } }

        // ── handlers ─────────────────────────────────────────────────────────────

        public void AddHandler(EventInvocation handler) => ChainAdd(handler);

        public void RemoveHandler(EventInvocation handler) => ChainRemove(handler);

        /// <summary>Programmatic subscribers only (excludes bound actions and the anchor).</summary>
        public bool HasSubscribers => Entries().Any(e => e.Target is not EventType and not ActionBinding);

        /// <summary>Anything that would run on a trigger: subscribers OR bound actions —
        /// preserves the HasEventHandlers conflation (NetworkServiceWatch.CanTriggerDemand).</summary>
        public bool HasHandlers => Entries().Any(e => e.Target is not EventType);

        /// <summary>Aborts all pending scheduled/throttled invocations of this event.</summary>
        public void CancelActions()
        {
            foreach (var binding in Actions)
                binding.Cancel();
        }

        // ── engine internals ─────────────────────────────────────────────────────

        internal IEnumerable<Delegate> Entries() => ChainGet()?.GetInvocationList() ?? [];

        /// <summary>The engine's sentinel handler: seeded at index 0 of the invocation
        /// list, it observes every invocation — including raw out-of-band Invoke calls
        /// that bypass the pipeline, which it reports instead of hiding.</summary>
        internal Task Anchor(Event @event)
        {
            Interlocked.Increment(ref _triggerCount);

            if (!EventMetaObject.InPipeline)
                Owner.ReportBypassedInvocation(this, @event);

            return Task.CompletedTask;
        }

        internal void SeedAnchor()
        {
            _anchor = Anchor;

            ChainUpdate(chain => (EventInvocation?)Delegate.Combine(_anchor, chain));
        }

        /// <summary>§7.4: subscribers PRESENT AT ORPHAN TIME are dropped — orphans replay
        /// bound actions only. Keeps the anchor and the binding wrappers. (A subscription
        /// added after orphaning is honored — documented in the spec.)</summary>
        internal void StripSubscribers()
        {
            ChainUpdate(chain =>
            {
                var kept = (chain?.GetInvocationList() ?? []).Where(e => e.Target is EventType or ActionBinding).ToArray();

                return kept.Length > 0 ? (EventInvocation?)Delegate.Combine(kept) : null;
            });
        }

        /// <summary>Atomic chain transformation — a concurrent += must never be clobbered
        /// by a strip/reseed (DeclaredEventType overrides with a CAS loop).</summary>
        private protected virtual void ChainUpdate(Func<EventInvocation?, EventInvocation?> transform)
            => ChainSet(transform(ChainGet()));

        /// <summary>Direct in-class assignment to a field-like event (`Ping = null;`) is
        /// legal C# and silently destroys the anchor — repair it before every pipeline
        /// run so diagnostics and the delegate extensions keep working.</summary>
        internal void EnsureAnchor()
        {
            if (ChainGet()?.GetInvocationList() is [{ Target: EventType }, ..])
                return;

            SeedAnchor();

            Owner.ReportAnchorReseeded(this);
        }

        internal abstract EventInvocation? ChainGet();
        private protected abstract void ChainSet(EventInvocation? chain);
        private protected abstract void ChainAdd(EventInvocation handler);
        private protected abstract void ChainRemove(EventInvocation handler);
    }

    /// <summary>Reflection-backed CLR event — storage is the compiler-generated backing
    /// field; add/remove go through the (possibly custom) accessors, exactly as today.</summary>
    internal sealed class CLREventType(EventMetaObject owner, string name, EventDescriptor descriptor, EventInfo info, FieldInfo field)
        : EventType(owner, name, descriptor)
    {
        internal override EventInvocation? ChainGet() => field.GetValue(Owner) as EventInvocation;

        private protected override void ChainSet(EventInvocation? chain) => field.SetValue(Owner, chain);

        private protected override void ChainAdd(EventInvocation handler) => info.GetAddMethod(true)?.Invoke(Owner, [handler]);

        private protected override void ChainRemove(EventInvocation handler) => info.GetRemoveMethod(true)?.Invoke(Owner, [handler]);
    }

    /// <summary>Runtime-declared event — owns its invocation list; no reflection (AOT-neutral).</summary>
    internal sealed class DynamicEventType(EventMetaObject owner, string name, EventDescriptor descriptor)
        : EventType(owner, name, descriptor)
    {
        private EventInvocation? _chain;

        internal override EventInvocation? ChainGet() => Volatile.Read(ref _chain);

        private protected override void ChainSet(EventInvocation? chain) => Volatile.Write(ref _chain, chain);

        private protected override void ChainAdd(EventInvocation handler)
        {
            EventInvocation? current, combined;
            do
            {
                current = Volatile.Read(ref _chain);
                combined = (EventInvocation?)Delegate.Combine(current, handler);
            }
            while (!ReferenceEquals(Interlocked.CompareExchange(ref _chain, combined, current), current));
        }

        private protected override void ChainRemove(EventInvocation handler)
        {
            EventInvocation? current, removed;
            do
            {
                current = Volatile.Read(ref _chain);
                removed = (EventInvocation?)Delegate.Remove(current, handler);
            }
            while (!ReferenceEquals(Interlocked.CompareExchange(ref _chain, removed, current), current));
        }

        private protected override void ChainUpdate(Func<EventInvocation?, EventInvocation?> transform)
        {
            EventInvocation? current, updated;
            do
            {
                current = Volatile.Read(ref _chain);
                updated = transform(current);
            }
            while (!ReferenceEquals(Interlocked.CompareExchange(ref _chain, updated, current), current));
        }
    }

    /// <summary>Registry entry for EventFilter members: enumeration, diagnostics and
    /// relation declarations only — filters have no actions and no trigger pipeline;
    /// folding happens in the statically-generic Filter extensions (AOT constraint).</summary>
    public sealed class FilterEventType(EventMetaObject owner, string name, EventDescriptor descriptor, FieldInfo field)
        : EventTypeBase(owner, name, descriptor)
    {
        // captured explicitly: under LangVersion preview a `=> field` body would bind
        // to the C# `field` KEYWORD (an unassigned synthesized backing field), not the
        // primary-constructor parameter
        internal FieldInfo Field { get; } = field;
    }
}
