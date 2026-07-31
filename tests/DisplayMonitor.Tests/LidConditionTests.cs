using MadWizard.Desomnia.Configuration.Binding;
using MadWizard.Desomnia.Display.Environments;
using Xunit;

namespace MadWizard.Desomnia.Display.Tests
{
    public class LidConditionTests
    {
        private static LidCondition CreateCondition(string value, FakeDisplayBuiltIn? builtIn)
            => new(new FakeDisplayManager { BuiltIn = builtIn }, value);

        [Theory]
        [InlineData("open", true, true)]
        [InlineData("OPEN", true, true)]
        [InlineData("Open", false, false)]
        [InlineData("closed", false, true)]
        [InlineData("CLOSED", false, true)]
        [InlineData("Closed", true, false)]
        public void MatchesTheRequiredLidState_CaseInsensitively(string value, bool lidOpen, bool expected)
        {
            var condition = CreateCondition(value, new FakeDisplayBuiltIn { LidOpen = lidOpen });

            Assert.Equal(expected, condition.IsSatisfied());
        }

        [Theory]
        [InlineData("ajar")]
        [InlineData("opened")]
        [InlineData("true")]
        [InlineData("")]
        public void InvalidValue_Throws(string value)
        {
            Assert.Throws<ConfigurationValueException>(() => CreateCondition(value, new FakeDisplayBuiltIn()));
        }

        [Theory]
        [InlineData("open")]
        [InlineData("closed")]
        public void WithoutBuiltInPanel_NeverSatisfied(string value)
        {
            var condition = CreateCondition(value, builtIn: null);

            Assert.False(condition.IsSatisfied());
        }

        [Theory]
        [InlineData("open")]
        [InlineData("closed")]
        public void UnknownLidState_NeverSatisfied(string value)
        {
            var condition = CreateCondition(value, new FakeDisplayBuiltIn { LidOpen = null });

            Assert.False(condition.IsSatisfied());
        }

        [Fact]
        public void Changed_RelaysLidTransitions()
        {
            var builtIn = new FakeDisplayBuiltIn { LidOpen = true };
            var condition = CreateCondition("open", builtIn);

            int raised = 0;
            EventHandler handler = (_, _) => raised++;

            Assert.Equal(0, builtIn.LidSubscriberCount); // lazy: nothing attached until someone listens

            condition.Changed += handler;
            Assert.Equal(1, builtIn.LidSubscriberCount);

            builtIn.FlipLid(false);
            Assert.Equal(1, raised);

            builtIn.FlipLid(true);
            Assert.Equal(2, raised);

            condition.Changed -= handler;
            Assert.Equal(0, builtIn.LidSubscriberCount);

            builtIn.FlipLid(false);
            Assert.Equal(2, raised); // no relay after the last handler was removed
        }

        [Fact]
        public void Changed_KeepsSinglePanelSubscription_UntilLastHandlerRemoved()
        {
            var builtIn = new FakeDisplayBuiltIn { LidOpen = true };
            var condition = CreateCondition("open", builtIn);

            int raisedA = 0, raisedB = 0;
            EventHandler handlerA = (_, _) => raisedA++;
            EventHandler handlerB = (_, _) => raisedB++;

            condition.Changed += handlerA;
            condition.Changed += handlerB;
            Assert.Equal(1, builtIn.LidSubscriberCount); // one relay subscription, not one per handler

            builtIn.FlipLid(false);
            Assert.Equal(1, raisedA);
            Assert.Equal(1, raisedB);

            condition.Changed -= handlerA;
            Assert.Equal(1, builtIn.LidSubscriberCount); // still one handler listening

            builtIn.FlipLid(true);
            Assert.Equal(1, raisedA);
            Assert.Equal(2, raisedB);

            condition.Changed -= handlerB;
            Assert.Equal(0, builtIn.LidSubscriberCount);
        }

        [Fact]
        public void Changed_WithoutBuiltInPanel_IsInert()
        {
            var condition = CreateCondition("open", builtIn: null);

            EventHandler handler = (_, _) => { };

            condition.Changed += handler; // must not throw on a machine without a lid
            condition.Changed -= handler;
        }
    }
}
