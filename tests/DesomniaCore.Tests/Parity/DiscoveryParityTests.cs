using Xunit;
using MadWizard.Desomnia.Events;

#pragma warning disable CS0067

namespace MadWizard.Desomnia.Tests.Parity
{
    /// <summary>§9.1: reflection discovery scope — public + non-public instance CLR events
    /// and [ActionHandler] methods; duplicate handler names fail fast.</summary>
    public class DiscoveryParityTests
    {
        [Fact]
        public void PublicEventIsDiscovered()
        {
            var actor = new TestEvents();

            Assert.False(actor.HasEventHandlers("Alpha")); // known, but no handlers yet

            actor.AddEventHandler("Alpha", _ => Task.CompletedTask);

            Assert.True(actor.HasEventHandlers("Alpha"));
        }

        [Fact]
        public void PrivateEventOnConcreteTypeIsDiscovered()
        {
            var actor = new TestEvents();

            actor.AddEventHandler("Secret", _ => Task.CompletedTask);

            Assert.True(actor.HasEventHandlers("Secret"));
        }

        [Fact]
        public async Task InheritedPublicEventIsDiscoveredAndTriggerable()
        {
            // backing field is private on the BASE class — discovery must walk the hierarchy
            var actor = new ContextActor();

            actor.AddEventHandler("Alpha", e => { actor.Record("handled"); return Task.CompletedTask; });

            await actor.DoTriggerAsync("Alpha");

            Assert.Equal(["handled"], actor.Snapshot());
        }

        [Fact]
        public void UnknownEventNameThrowsOnHandlerWiring()
        {
            var actor = new TestEvents();

            Assert.Throws<KeyNotFoundException>(() => actor.AddEventHandler("NoSuchEvent", _ => Task.CompletedTask));
            Assert.Throws<KeyNotFoundException>(() => actor.HasEventHandlers("NoSuchEvent"));
        }

        [Fact]
        public void RemovedHandlerNoLongerCounts()
        {
            var actor = new TestEvents();
            EventInvocation handler = _ => Task.CompletedTask;

            actor.AddEventHandler("Alpha", handler);
            actor.RemoveEventHandler("Alpha", handler);

            Assert.False(actor.HasEventHandlers("Alpha"));
        }

        private class DuplicateHandlerActor : EventMetaObject
        {
            [ActionHandler("same")] private void First() { }
            [ActionHandler("same")] private void Second() { }
        }

        [Fact]
        public void DuplicateActionHandlerNamesFailFastAtConstruction()
        {
            Assert.Throws<ArgumentException>(() => new DuplicateHandlerActor());
        }

        private class BaseHandlerActor : EventMetaObject
        {
            [ActionHandler("inherited")] private void Handle() => Invoked = true;
            public bool Invoked;
        }

        private class DerivedHandlerActor : BaseHandlerActor { }

        [Fact]
        public async Task PrivateActionHandlerOnBaseClassIsDiscovered()
        {
            var actor = new DerivedHandlerActor();

            var handled = await actor.TryHandleEventAction(new Event("X"), Actions.Command("inherited"));

            Assert.True(handled);
            Assert.True(actor.Invoked);
        }

        [Fact]
        public async Task ManualConstructionOutsideAnyContainerWorks()
        {
            // The DuoInstance pattern (DuoManager.cs:115): plain `new`, ctor-time wiring,
            // triggers from ordinary code — all functional without any DI involvement.
            var actor = new TestEvents();
            actor.AddEventAction("Alpha", Actions.Named("noop"));

            await actor.DoTriggerAsync("Alpha");

            Assert.Equal(1, actor.Count("noop"));
        }

        private class CtorWiredActor : EventMetaObject
        {
            public readonly List<string> Log = [];

            public event EventInvocation? Ready;

            public CtorWiredActor()
            {
                // the DuoInstance shape (DuoInstance.cs:19-25): wiring — including null
                // actions from unset config attributes — and triggering inside the ctor
                Ready.Meta.AddHandler(_ => { Log.Add("handler"); return Task.CompletedTask; });
                Ready.AddAction(Actions.Named("mark"));
                Ready.AddAction((Configuration.ActionInfo?)null);

                Ready.TriggerEvent();
            }

            [ActionHandler("mark")]
            private void Mark() => Log.Add("action");
        }

        [Fact]
        public void ConstructorTimeWiringAndTriggeringWorks()
        {
            // base-ctor self-sufficiency (spec §2): the registry exists before any
            // derived ctor body runs, so everything already ran when `new` returns
            var actor = new CtorWiredActor();

            Assert.Equal(["handler", "action"], actor.Log);
        }

        [Fact]
        public void PrivateBaseClassEventIsNotDiscoveredOnDerivedTypes()
        {
            // GetEvents omits base-private events (unlike the hierarchy-walking field
            // lookup) — TestEvents.Secret is invisible on a ContextActor instance
            var actor = new ContextActor();

            Assert.Throws<KeyNotFoundException>(() => actor.AddEventHandler("Secret", _ => Task.CompletedTask));
        }

        private class MixedDelegatesActor : EventMetaObject
        {
            public event EventInvocation? Real;
            public event EventHandler? Plain;
            public event Func<bool>? Query;
        }

        [Fact]
        public void NonEventInvocationDelegatesAreInvisibleToTheRegistry()
        {
            // the inclusion filter is delegate-type-name based (EventSource.cs:13) —
            // EventHandler/Func events (e.g. ResourceMonitor.TrackingStarted/Filters)
            // are NOT part of the event system, and the redesign must not widen this
            var actor = (IEventSystem)new MixedDelegatesActor();

            actor["Real"].AddHandler(_ => Task.CompletedTask);
            Assert.Throws<KeyNotFoundException>(() => actor["Plain"]);
            Assert.Throws<KeyNotFoundException>(() => actor["Query"]);
        }

        private class InlineInitActor : EventMetaObject
        {
            public static readonly List<string> StaticLog = [];

            public event EventInvocation? Preset = _ => { StaticLog.Add("inline"); return Task.CompletedTask; };

            public void Fire() => Preset.TriggerEvent();
        }

        [Fact]
        public void InlineInitializedEventSubscriberIsPreservedAndRunsFirst()
        {
            // derived field initializers run before the base ctor — discovery must not
            // disturb a pre-populated backing field (spec §4.3: anchors combine IN FRONT)
            InlineInitActor.StaticLog.Clear();
            var actor = new InlineInitActor();
            var preset = ((IEventSystem)actor)["Preset"];

            Assert.True(preset.HasHandlers);                 // subscribed from birth

            preset.AddHandler(_ => { InlineInitActor.StaticLog.Add("added"); return Task.CompletedTask; });
            actor.Fire();

            Assert.Equal(["inline", "added"], InlineInitActor.StaticLog);
        }
    }
}
