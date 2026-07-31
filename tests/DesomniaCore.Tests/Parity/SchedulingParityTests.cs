using Xunit;
using MadWizard.Desomnia.Events;

namespace MadWizard.Desomnia.Tests.Parity
{
    /// <summary>§9.1: scheduled/throttled semantics — first-event-wins, re-trigger ignored
    /// (not restarted) while pending, throttle counts triggers (arming trigger excluded),
    /// cancellation of BOTH pending kinds, cancel-all on dispose, per-event independence.</summary>
    public class SchedulingParityTests
    {
        // Negative proofs ("must NOT fire") race a follow-up step against the pending
        // window: the window must comfortably exceed plausible scheduler stalls, and the
        // settle time must exceed the window. Positive fires poll and stay fast.
        private const int Window = 750;

        [Fact]
        public async Task ScheduledActionInfoFiresAfterDelayNotImmediately()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", Window));

            await actor.DoTriggerAsync("Alpha");
            Assert.Equal(0, actor.Count("noop"));            // arming is synchronous, firing is not

            await Wait.Until(() => actor.Count("noop") == 1);
        }

        [Fact]
        public async Task FirstEventWinsAndReTriggerIsIgnoredWhilePending()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", Window));

            var first = new Event("Alpha");
            var second = new Event("Alpha");

            await actor.DoTriggerAsync(first);
            await actor.DoTriggerAsync(second);              // ignored: slot already armed

            await Wait.Until(() => actor.Count("noop") == 1);
            await Wait.Settle();

            Assert.Equal(1, actor.Count("noop"));            // exactly once
            Assert.Same(first, actor.LastActionEvent);       // with the FIRST event object
        }

        [Fact]
        public async Task ReTriggerDoesNotRestartThePendingDelay()
        {
            // Ignore-semantics vs restart-semantics: under continuous re-triggering
            // (faster than the delay) restart would postpone the action forever; the
            // current engine fires anyway. The poll below re-triggers on every probe.
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", 400));

            var first = new Event("Alpha");
            await actor.DoTriggerAsync(first);

            await Wait.Until(() => { actor.DoTrigger("Alpha"); return actor.Count("noop") >= 1; }, 8000);

            actor.DoCancel("Alpha");                         // drop the re-armed follower
            Assert.Same(first, actor.LastActionEvent);
        }

        [Fact]
        public async Task SlotIsFreedAfterFiringAndCanArmAgain()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", 100));

            await actor.DoTriggerAsync("Alpha");
            await Wait.Until(() => actor.Count("noop") == 1);

            // the slot is freed in the fire task's finally — retry the trigger until it lands
            await Wait.Until(() => { actor.DoTrigger("Alpha"); return actor.Count("noop") >= 2; });
        }

        [Fact]
        public async Task CancelEventActionAbortsThePendingScheduledInvocation()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", Window));

            await actor.DoTriggerAsync("Alpha");
            actor.DoCancel("Alpha");

            await Wait.SettleAfter(Window);
            Assert.Equal(0, actor.Count("noop"));
        }

        [Fact]
        public async Task CancelEventActionAbortsThePendingThrottledInvocation()
        {
            // The throttled path cancels via SemaphoreSlim.WaitAsync → OperationCanceledException
            // (Actor.cs:98) — a different path than the scheduled TaskCanceledException.
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Throttled("noop", 1));

            await actor.DoTriggerAsync("Alpha");             // arms
            actor.DoCancel("Alpha");
            await actor.DoTriggerAsync("Alpha");             // would fire on a live slot

            await Wait.Settle(500);
            Assert.Equal(0, actor.Count("noop"));
        }

        [Fact]
        public async Task DisposeCancelsAllPendingScheduledInvocations()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", Window));
            actor.AddEventAction("Beta", Actions.Delayed("noop2", Window));

            await actor.DoTriggerAsync("Alpha");
            await actor.DoTriggerAsync("Beta");
            actor.Dispose();

            await Wait.SettleAfter(Window);
            Assert.Equal(0, actor.Count("noop"));
            Assert.Equal(0, actor.Count("noop2"));
        }

        [Fact]
        public async Task DisposeCancelsAPendingThrottledInvocation()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Throttled("noop", 1));

            await actor.DoTriggerAsync("Alpha");             // arms
            actor.Dispose();
            await actor.DoTriggerAsync("Alpha");             // a surviving slot would fire now

            await Wait.Settle(500);
            Assert.Equal(0, actor.Count("noop"));
        }

        [Fact]
        public async Task ThrottledActionInfoFiresAfterNFurtherTriggers()
        {
            // "+2x": the arming trigger does NOT count; the action fires on the 2nd
            // trigger AFTER it (3rd overall), with the FIRST event object. Throttling is
            // trigger-counted, not timed — the settles below cannot false-fail on stalls.
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Throttled("noop", 2));

            var first = new Event("Alpha");

            await actor.DoTriggerAsync(first);               // arms (does not count)
            await Wait.Settle(150);
            Assert.Equal(0, actor.Count("noop"));

            await actor.DoTriggerAsync("Alpha");             // 1st counted trigger
            await Wait.Settle(150);
            Assert.Equal(0, actor.Count("noop"));

            await actor.DoTriggerAsync("Alpha");             // 2nd counted trigger → fires

            await Wait.Until(() => actor.Count("noop") == 1);
            Assert.Same(first, actor.LastActionEvent);
        }

        [Fact]
        public async Task ThrottledSlotReArmsAfterFiring()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Throttled("noop", 1));

            await actor.DoTriggerAsync("Alpha");             // arm
            await actor.DoTriggerAsync("Alpha");             // fire
            await Wait.Until(() => actor.Count("noop") == 1);

            // next cycle: re-arm + fire again (retry until the freed slot accepts)
            await Wait.Until(() => { actor.DoTrigger("Alpha"); return actor.Count("noop") >= 2; });
        }

        [Fact]
        public async Task ZeroDelayScheduledActionInfoRunsSynchronously()
        {
            // Delay == Zero (the "+0s" default of ScheduledActionInfoConverter) takes the
            // plain immediate path, not the slot.
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", 0));

            await actor.DoTriggerAsync("Alpha");

            Assert.Equal(1, actor.Count("noop"));
        }

        [Fact]
        public async Task ZeroTimesThrottledActionInfoRunsSynchronously()
        {
            // Times == 0 (the "+0x" default of ThrottledActionInfoConverter) likewise.
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Throttled("noop", 0));

            await actor.DoTriggerAsync("Alpha");

            Assert.Equal(1, actor.Count("noop"));
        }

        [Fact]
        public async Task MultipleDelayedActionInfosOnOneEventAllFire()
        {
            // flipped quirk (phase 1): per-binding scheduling state — the old
            // name-keyed slot starved every delayed action after the first
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", 100));
            actor.AddEventAction("Alpha", Actions.Delayed("noop2", 100));

            await actor.DoTriggerAsync("Alpha");

            await Wait.Until(() => actor.Count("noop") == 1 && actor.Count("noop2") == 1);
        }

        [Fact]
        public async Task PendingsOnDifferentEventsAreIndependent()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", 100));
            actor.AddEventAction("Beta", Actions.Delayed("noop2", 100));

            await actor.DoTriggerAsync("Alpha");
            await actor.DoTriggerAsync("Beta");

            await Wait.Until(() => actor.Count("noop") == 1 && actor.Count("noop2") == 1);
        }

        [Fact]
        public async Task TriggeringAnotherEventDoesNotDisturbAPending()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", 200));
            actor.AddEventAction("Beta", Actions.Named("noop2"));

            await actor.DoTriggerAsync("Alpha");
            await actor.DoTriggerAsync("Beta");              // unrelated event, no cancel relation

            Assert.Equal(1, actor.Count("noop2"));
            await Wait.Until(() => actor.Count("noop") == 1);
        }

        [Fact]
        public async Task CancelingAnotherEventDoesNotDisturbAPending()
        {
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Delayed("noop", 200));

            await actor.DoTriggerAsync("Alpha");
            actor.DoCancel("Beta");                          // sibling event — must not touch Alpha

            await Wait.Until(() => actor.Count("noop") == 1);
        }
    }
}
