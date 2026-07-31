using Xunit;
using MadWizard.Desomnia.Events;

namespace MadWizard.Desomnia.Tests.Parity
{
    /// <summary>§9.1: trigger semantics — sync blocking, sequential await, add-order
    /// interleaving of subscribers and actions, type stamping, silent unknown no-op,
    /// context harvest, multi-owner re-trigger.</summary>
    public class TriggerParityTests
    {
        [Fact]
        public void SyncTriggerBlocksUntilAsyncHandlersComplete()
        {
            var actor = new TestEvents();
            actor.AddEventHandler("Alpha", async _ => { await Task.Delay(100); actor.Record("done"); });

            actor.DoTrigger("Alpha");
            Assert.Equal(["done"], actor.Snapshot());

            actor.DoTrigger("Alpha", new Event());   // the (name, Event) sync overload blocks too
            Assert.Equal(["done", "done"], actor.Snapshot());
        }

        [Fact]
        public async Task HandlersAreAwaitedSequentially()
        {
            var actor = new TestEvents();
            actor.AddEventHandler("Alpha", async _ => { await Task.Delay(80); actor.Record("slow"); });
            actor.AddEventHandler("Alpha", _ => { actor.Record("fast"); return Task.CompletedTask; });

            await actor.DoTriggerAsync("Alpha");

            Assert.Equal(["slow", "fast"], actor.Snapshot());
        }

        [Fact]
        public async Task SubscribersAndBoundActionsInterleaveInAddOrder()
        {
            // Today AddEventAction registers through the same invocation list as any
            // subscriber (Actor.cs:74/110/115) — order is strictly add order, NOT
            // "handlers first, then actions".
            var actor = new TestEvents();
            actor.AddEventHandler("Alpha", _ => { actor.Record("sub1"); return Task.CompletedTask; });
            actor.AddEventAction("Alpha", Actions.Named("noop"));
            actor.AddEventHandler("Alpha", _ => { actor.Record("sub2"); return Task.CompletedTask; });

            await actor.DoTriggerAsync("Alpha");

            Assert.Equal(["sub1", "noop", "sub2"], actor.Snapshot());
        }

        [Fact]
        public async Task TriggerWithNameStampsEventType()
        {
            var actor = new TestEvents();
            var @event = new Event("Original");

            Event? seen = null;
            actor.AddEventHandler("Alpha", e => { seen = e; return Task.CompletedTask; });

            await actor.DoTriggerAsync("Alpha", @event);

            Assert.Same(@event, seen);
            Assert.Equal("Alpha", @event.Type);   // silently renamed to the trigger name
        }

        [Fact]
        public void EventDefaultsAreStable()
        {
            var @event = new Event();

            Assert.Equal("unknown", @event.Type);
            Assert.Throws<ArgumentNullException>(() => @event.AddContext(null!));
        }

        [Fact]
        public async Task SourceIsStampedAndContextHarvested()
        {
            var actor = new ContextActor { Payload = "ctx-value" };
            Event? seen = null;
            actor.AddEventHandler("Alpha", e => { seen = e; return Task.CompletedTask; });

            await actor.DoTriggerAsync("Alpha");

            Assert.NotNull(seen);
            Assert.Same(actor, seen!.Source);
            Assert.Equal(seen, seen.Context.First());          // the event itself leads
            Assert.Same(actor, seen.Context.Skip(1).First());  // then the source
            Assert.Contains("ctx-value", seen.Context);        // then [EventContext] values
        }

        [Fact]
        public async Task NonPublicEventContextPropertiesAreHarvested()
        {
            // the harvest scans NonPublic properties too (EventSource.cs:101), matching
            // the non-public discovery scope of events and handlers
            var actor = new ContextActor();
            actor.SetHidden(new Version(1, 2));
            Event? seen = null;
            actor.AddEventHandler("Alpha", e => { seen = e; return Task.CompletedTask; });

            await actor.DoTriggerAsync("Alpha");

            Assert.Contains(new Version(1, 2), seen!.Context);
        }

        [Fact]
        public async Task NullEventContextPropertiesAreSkipped()
        {
            var actor = new ContextActor { Payload = null };
            Event? seen = null;
            actor.AddEventHandler("Alpha", e => { seen = e; return Task.CompletedTask; });

            await actor.DoTriggerAsync("Alpha");

            Assert.DoesNotContain(seen!.Context, c => c is string);
        }

        [Fact]
        public async Task RepeatedTriggerDoesNotDuplicateContexts()
        {
            var actor = new ContextActor { Payload = "stable" };
            var @event = new Event("Alpha");
            actor.AddEventHandler("Alpha", _ => Task.CompletedTask);

            await actor.DoTriggerAsync(@event);
            await actor.DoTriggerAsync(@event);

            Assert.Single(@event.Context.OfType<string>());   // HashSet dedups the same instance
        }

        [Fact]
        public async Task MultiOwnerReTriggerRestampsSourceAndAccumulatesContext()
        {
            // HostDemandWatch.ReportDemand re-triggers the identical Event object on a
            // second owner (HostDemandWatch.cs:217-221) — this must stay legal.
            var first = new ContextActor { Payload = "from-first" };
            var second = new ContextActor { Payload = "from-second" };
            first.AddEventHandler("Alpha", _ => Task.CompletedTask);
            second.AddEventHandler("Beta", _ => Task.CompletedTask);

            var @event = new Event("Alpha");

            await first.DoTriggerAsync(@event);
            Assert.Same(first, @event.Source);

            await second.DoTriggerAsync("Beta", @event);

            Assert.Same(second, @event.Source);               // restamped
            Assert.Equal("Beta", @event.Type);                // renamed
            Assert.Contains("from-first", @event.Context);    // accumulated
            Assert.Contains("from-second", @event.Context);
        }
    }
}
