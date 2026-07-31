using MadWizard.Desomnia.Display.Configuration;
using MadWizard.Desomnia.Display.Watch;
using Xunit;

namespace MadWizard.Desomnia.Display.Tests
{
    /// <summary>
    /// The <c>disabled</c> attribute follows the preventIdle inheritance model: external
    /// displays coalesce descriptor over monitor default (null inherits), while the
    /// built-in panel deliberately does NOT inherit the monitor-level value.
    /// </summary>
    public class DisabledConfigurationTests
    {
        private static bool EffectiveDisabled(DisplayMonitorConfig config, DisplayWatchDescriptor? desc)
        {
            using var watch = new DisplayWatchExternal(new FakeDisplayExternal());

            watch.ApplyConfiguration(config, desc);

            return watch.ShouldBeDisabled;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void External_WithoutDescriptorValue_InheritsMonitorDefault(bool monitorDisabled)
        {
            var config = new DisplayMonitorConfig { Disabled = monitorDisabled };

            Assert.Equal(monitorDisabled, EffectiveDisabled(config, desc: null));                       // no matching descriptor
            Assert.Equal(monitorDisabled, EffectiveDisabled(config, new DisplayWatchDescriptor()));     // descriptor without disabled=
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void External_DescriptorValue_OverridesMonitorDefault(bool monitorDisabled, bool descriptorDisabled)
        {
            var config = new DisplayMonitorConfig { Disabled = monitorDisabled };
            var desc = new DisplayWatchDescriptor { Disabled = descriptorDisabled };

            Assert.Equal(descriptorDisabled, EffectiveDisabled(config, desc));
        }

        [Fact]
        public void External_Configure_AppliesTheMatchingDescriptor()
        {
            var display = new FakeDisplayExternal(); // vendor "DEL"

            var config = new DisplayMonitorConfig
            {
                Disabled = true,
                Display =
                {
                    new DisplayWatchDescriptor { Vendor = "GSM", Disabled = true },   // different display
                    new DisplayWatchDescriptor { Vendor = "DEL", Disabled = false },  // overrides the monitor default
                },
            };

            using var watch = new DisplayWatchExternal(display);

            config.Configure(display, watch.ApplyConfiguration); // the DisplayMonitor's own seam

            Assert.False(watch.ShouldBeDisabled);
        }

        [Fact]
        public void External_Configure_WithoutAnyDescriptor_UsesMonitorDefault()
        {
            var display = new FakeDisplayExternal();

            var config = new DisplayMonitorConfig { Disabled = true };

            using var watch = new DisplayWatchExternal(display);

            config.Configure(display, watch.ApplyConfiguration);

            Assert.True(watch.ShouldBeDisabled);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BuiltIn_DoesNotInheritMonitorDefault(bool monitorDisabled)
        {
            // the monitor-level default must not leak onto the panel: its watch is built
            // from the descriptor alone, whose non-nullable Disabled defaults to false
            var config = new DisplayMonitorConfig
            {
                Disabled = monitorDisabled,
                DisplayBuiltIn = new DisplayBuiltInDescriptor(),
            };

            using var watch = new DisplayWatchBuiltIn(new FakeDisplayBuiltIn(), config.DisplayBuiltIn!);

            Assert.False(watch.ShouldBeDisabled);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BuiltIn_UsesItsOwnDisabledValue(bool disabled)
        {
            var desc = new DisplayBuiltInDescriptor { Disabled = disabled };

            using var watch = new DisplayWatchBuiltIn(new FakeDisplayBuiltIn(), desc);

            Assert.Equal(disabled, watch.ShouldBeDisabled);
        }
    }
}
