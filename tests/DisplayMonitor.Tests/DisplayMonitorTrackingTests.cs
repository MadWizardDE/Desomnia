using Autofac;
using MadWizard.Desomnia.Display.Configuration;
using MadWizard.Desomnia.Display.Watch;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MadWizard.Desomnia.Display.Tests
{
    /// <summary>
    /// The <see cref="DisplayMonitor"/> subscribes the manager's events BEFORE its initial
    /// enumeration, so a display can be delivered through both the startup snapshot and a
    /// connect event: tracking must absorb the duplicate, and a display whose disconnect
    /// already fired (finding no watch to tear down) must not be adopted afterwards.
    /// </summary>
    public class DisplayMonitorTrackingTests
    {
        private static async Task<(DisplayMonitor Monitor, IContainer Container)> StartMonitor(
            FakeDisplayManager manager, DisplayMonitorConfig? config = null)
        {
            var container = new ContainerBuilder().Build();

            var monitor = new DisplayMonitor(manager, config ?? new DisplayMonitorConfig())
            {
                Logger = NullLogger<DisplayMonitor>.Instance,
                Scope = container,
            };

            await ((IHostedService)monitor).StartAsync(CancellationToken.None);

            return (monitor, container);
        }

        [Fact]
        public async Task Display_DeliveredByEnumerationAndConnectEvent_IsTrackedOnce()
        {
            var display = new FakeDisplayExternal();
            var manager = new FakeDisplayManager { Displays = { display } };

            var (monitor, container) = await StartMonitor(manager);

            using (container)
            {
                manager.RaiseConnected(display); // the same instance arrives through the event path too

                Assert.Single(monitor.OfType<DisplayWatchExternal>());
            }
        }

        [Fact]
        public async Task Display_AlreadyDisconnectedAgain_IsNotAdopted()
        {
            // the startup snapshot can still contain a display whose disconnect event already
            // ran (and found no watch) — tracking it now would strand a watch forever
            var display = new FakeDisplayExternal { IsConnected = false };
            var manager = new FakeDisplayManager { Displays = { display } };

            var (monitor, container) = await StartMonitor(manager);

            using (container)
            {
                Assert.Empty(monitor.OfType<DisplayWatchExternal>());
            }
        }

        [Fact]
        public async Task ConnectEvent_TracksTheDisplay_AndReleasesItsStaleIntent()
        {
            var display = new FakeDisplayExternal { ShouldBeDisabled = true }; // left behind by a predecessor
            var manager = new FakeDisplayManager();

            var (monitor, container) = await StartMonitor(manager);

            using (container)
            {
                manager.RaiseConnected(display);

                Assert.Single(monitor.OfType<DisplayWatchExternal>());
                Assert.False(display.ShouldBeDisabled); // watched with disabled=false -> released
            }
        }
    }
}
