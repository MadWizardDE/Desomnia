using Xunit;

namespace MadWizard.Desomnia.Tests.Parity
{
    /// <summary>§9.1: AddEventAction wiring contract — null/blank no-op, KeyNotFoundException
    /// on unknown events, actions count as handlers (HasEventHandlers conflation).</summary>
    public class WiringParityTests
    {
        [Fact]
        public void NullActionIsIgnored()
        {
            var actor = new TestEvents();

            actor.AddEventAction("Alpha", null);

            Assert.False(actor.HasEventHandlers("Alpha"));
        }

        [Fact]
        public void BlankActionNameIsIgnored()
        {
            var actor = new TestEvents();

            actor.AddEventAction("Alpha", Actions.Named("  "));

            Assert.False(actor.HasEventHandlers("Alpha"));
        }

        [Fact]
        public void UnknownEventNameThrowsAtWiringTime()
        {
            var actor = new TestEvents();

            Assert.Throws<KeyNotFoundException>(() => actor.AddEventAction("NoSuchEvent", Actions.Named("noop")));
        }

        [Fact]
        public void NullActionOnUnknownEventDoesNotThrow()
        {
            // the null check precedes the registry lookup (Actor.cs:25-26) — config
            // objects with unset attributes may be wired against events that never exist
            var actor = new TestEvents();

            actor.AddEventAction("NoSuchEvent", null);
        }

        [Fact]
        public void BoundActionCountsAsEventHandler()
        {
            // NetworkServiceWatch.CanTriggerDemand (NetworkServiceWatch.cs:20) gates
            // WoL-on-service-demand on exactly this conflation — it must survive migration.
            var actor = new TestEvents();

            actor.AddEventAction("Alpha", Actions.Named("noop"));

            Assert.True(actor.HasEventHandlers("Alpha"));
        }

        [Fact]
        public void ScheduledActionInfoAlsoCountsAsEventHandler()
        {
            var actor = new TestEvents();

            actor.AddEventAction("Alpha", Actions.Delayed("noop", 60_000));

            Assert.True(actor.HasEventHandlers("Alpha"));
        }

        [Fact]
        public void ThrottledActionInfoAlsoCountsAsEventHandler()
        {
            var actor = new TestEvents();

            actor.AddEventAction("Alpha", Actions.Throttled("noop", 3));

            Assert.True(actor.HasEventHandlers("Alpha"));
        }

        [Fact]
        public async Task MultipleActionsOnOneEventAllRun()
        {
            var actor = new TestEvents();

            actor.AddEventAction("Alpha", Actions.Named("noop"));
            actor.AddEventAction("Alpha", Actions.Named("noop2"));

            await actor.DoTriggerAsync("Alpha");

            Assert.Equal(1, actor.Count("noop"));
            Assert.Equal(1, actor.Count("noop2"));
        }
    }
}
