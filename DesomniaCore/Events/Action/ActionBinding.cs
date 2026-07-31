using MadWizard.Desomnia.Configuration;

namespace MadWizard.Desomnia.Events
{
    /// <summary>
    /// One bound action of one event, owning its OWN scheduling state — multiple
    /// (including multiple delayed) actions per event are fully independent. The
    /// binding's wrapper delegate sits in the event's invocation list at its add-order
    /// position, so subscribers and actions interleave exactly as added.
    /// </summary>
    public sealed class ActionBinding
    {
        private readonly EventType _type;

        private readonly Lock _lock = new();
        private Pending? _pending;                            // at most one per binding
        private bool _retired;                                // set by RemoveAction — arming refused

        public EventAction Action { get; }

        internal ActionBinding(EventType type, EventAction action)
        {
            _type = type;
            Action = action;
        }

        internal EventInvocation Invocation => HandleAsync;   // Target == this → identifiable in the chain

        /// <summary>Aborts the pending scheduled/throttled invocation, if any.</summary>
        public void Cancel()
        {
            Pending? pending;

            lock (_lock)
            {
                pending = _pending;
                _pending = null;
            }

            pending?.Source.Cancel();                         // outside the lock: cancelled Task.Delay
        }                                                     // continuations can inline on this thread

        /// <summary>Permanently disables arming — a removed binding whose wrapper is
        /// still visible in an in-flight entry snapshot must never re-arm, or its
        /// pending would be unreachable by every cancellation path.</summary>
        internal void Retire()
        {
            lock (_lock)
                _retired = true;

            Cancel();
        }

        private sealed class Pending(Event @event)
        {
            public Event Event => @event;
            public CancellationTokenSource Source { get; } = new();
            public uint Remaining;
            public bool Fired;
        }

        private bool MayArm => !_retired && !_type.Owner.IsEngineDisposed;   // read under _lock

        private async Task HandleAsync(Event @event)
        {
            if (Action.Delay is TimeSpan delay)
            {
                Pending? armed = null;
                bool refused = false;

                lock (_lock)
                {
                    if (_pending == null)
                    {
                        if (MayArm)
                            _pending = armed = new Pending(@event);
                        else
                            refused = true;
                    }
                }

                if (armed != null)                            // re-trigger while pending: ignored, first event wins
                    _ = RunDelayedAsync(armed, delay);
                else if (refused)
                    _type.Owner.ReportRefusedArming(_type);   // a silently dropped pending must be visible
            }
            else if (Action.Times is uint times && times > 0) // Times == 0 → immediate (matches the "+0x" default)
            {
                Pending? fire = null;
                bool refused = false;

                lock (_lock)
                {
                    if (_pending == null)
                    {
                        if (MayArm)
                            _pending = new Pending(@event) { Remaining = times };   // arming trigger does not count
                        else
                            refused = true;
                    }
                    else if (!_pending.Fired && --_pending.Remaining == 0)
                    {
                        _pending.Fired = true;                // slot stays OCCUPIED through the fire:
                        fire = _pending;                      // re-triggers during execution are ignored
                    }
                }

                if (fire != null)
                    _ = RunDetachedFireAsync(fire);           // detached, like the legacy Task.Run
                else if (refused)
                    _type.Owner.ReportRefusedArming(_type);
            }
            else
            {
                await DispatchAsync(@event);                  // immediate: blocks the trigger, as today
            }
        }

        private async Task RunDelayedAsync(Pending pending, TimeSpan delay)
        {
            EventMetaObject.ExitPipelineFlow();               // detached work must not mute bypass diagnostics

            try
            {
                await Task.Delay(delay, pending.Source.Token);
            }
            catch (OperationCanceledException)
            {
                ClearIfCurrent(pending);
                return;
            }

            lock (_lock)
            {
                if (!ReferenceEquals(_pending, pending))      // cancelled/superseded just in time
                    return;

                pending.Fired = true;                         // slot stays occupied through the dispatch —
            }                                                 // re-triggers during execution are ignored

            await RunDetachedFireAsync(pending);
        }

        private async Task RunDetachedFireAsync(Pending pending)
        {
            EventMetaObject.ExitPipelineFlow();

            try
            {
                await DispatchAsync(pending.Event);
            }
            catch (Exception ex)
            {
                // the error chain was already consulted in DispatchAsync — on the
                // detached path an unhandled error has no caller to reach: surface it
                // in the log instead of losing it silently
                _type.Owner.ReportLostActionError(new ActionError(pending.Event, Action, ex));
            }
            finally
            {
                ClearIfCurrent(pending);                      // identity-guarded: a successor's slot
            }                                                 // must never be clobbered
        }

        private void ClearIfCurrent(Pending pending)
        {
            lock (_lock)
            {
                if (ReferenceEquals(_pending, pending))
                    _pending = null;
            }
        }

        /// <summary>Resolves and executes the action. Handler-execution errors are routed
        /// (once, unwrapped) by the executing actor; this method routes only resolution
        /// failures. Unhandled errors propagate unwrapped.</summary>
        private async Task DispatchAsync(Event @event)
        {
            if (await _type.Owner.DispatchActionAsync(@event, Action))
                return;

            var missing = new NotImplementedException($"action '{Action}' not found on {_type.Owner.GetType().Name} for event {@event}");

            if (!_type.Owner.RouteActionError(new ActionError(@event, Action, missing)))
                throw missing;
        }
    }
}
