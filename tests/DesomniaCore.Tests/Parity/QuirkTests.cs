using System.Reflection;
using Xunit;
using MadWizard.Desomnia.Events;

namespace MadWizard.Desomnia.Tests.Parity
{
    /// <summary>Historical home of the pins that flipped phase by phase (slot starvation,
    /// context consumption, error double-surface, lost delayed errors → phase 1;
    /// vetoed-cancel → phase 3; first-'+' split → phase 4; Bubbles deletion → phase 5).
    /// All flipped — what remains is a permanent pin of intended fail-loud behavior.</summary>
    public class QuirkTests
    {
        private class ExplicitAccessorActor : EventMetaObject
        {
            private EventInvocation? _custom;

            public event EventInvocation? Custom
            {
                add => _custom += value;
                remove => _custom -= value;
            }
        }

        [Fact]
        public void ExplicitAccessorEventCrashesTheConstructor()
        {
            // The event registry requires a backing field named like the event; an
            // explicit-accessor event has none, and the First() at EventSource.cs:15
            // throws an undescriptive InvalidOperationException from the ctor (the null
            // guard at :17 is dead code). §9.2: the redesign fails loudly with a
            // DESCRIPTIVE error instead.
            Assert.Throws<InvalidOperationException>(() => new ExplicitAccessorActor());
        }

    }
}
