using MadWizard.Desomnia.Network.Bridges;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Manager;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.NetworkInformation;
using Xunit;

using ConfiguredNetwork = (MadWizard.Desomnia.Network.Configuration.NetworkMonitorConfig Config,
    MadWizard.Desomnia.Network.Bridges.InterfaceMatcher Matcher,
    System.Collections.Generic.IReadOnlyList<(MadWizard.Desomnia.Network.Bridges.InterfaceMatcher Matcher, bool Force)> Blocks);

namespace MadWizard.Desomnia.Network.Tests
{
    /// <summary>
    /// The desired-state half of the NetworkInterfaceBlock: environment blocks outrank the
    /// monitors, the desired set unites both worlds, and the force flag ORs over every
    /// matching block.
    /// </summary>
    public class InterfaceBlockPlanningTests
    {
        [Fact]
        public void EnvironmentBlockedInterface_NeverBecomesACandidate()
        {
            var wifi = new FakeNetworkInterface("en0");
            var wired = new FakeNetworkInterface("en12");

            ConfiguredNetwork config = (new NetworkMonitorConfig { Name = "any" }, new InterfaceMatcher(), []);

            var envBlocked = DynamicNetworkObserver.ResolveEnvironmentBlocks(
                [wifi, wired], [(new InterfaceMatcher("en0$"), false)]);

            var candidates = DynamicNetworkObserver.SelectCandidates([wifi, wired], [config], envBlocked);

            var candidate = Assert.Single(candidates);

            Assert.Same(wired, candidate.Interface); // en0 matches the config, but the environment outranks it
        }

        [Fact]
        public void EnvironmentForce_ORsOverTheMatchingBlocks()
        {
            var wifi = new FakeNetworkInterface("en0");

            var envBlocked = DynamicNetworkObserver.ResolveEnvironmentBlocks(
                [wifi], [(new InterfaceMatcher("en0"), false), (new InterfaceMatcher("en."), true)]);

            Assert.True(envBlocked[wifi]);
        }

        [Fact]
        public void DesiredSet_UnitesEnvironmentAndLiveMonitorBlocks()
        {
            var wifi = new FakeNetworkInterface("en0");
            var slow = new FakeNetworkInterface("en7");
            var wired = new FakeNetworkInterface("en12");

            var envBlocked = DynamicNetworkObserver.ResolveEnvironmentBlocks(
                [wifi, slow, wired], [(new InterfaceMatcher("en7"), false)]);

            var desired = DynamicNetworkObserver.ComputeDesiredBlocks([wifi, slow, wired], envBlocked,
                [(wired, [(new InterfaceMatcher("en0"), false)])], NullLogger.Instance); // the monitor on en12 blocks en0

            Assert.Equal(["en0", "en7"], desired.Keys.Select(i => i.Identity.Id).Order());
        }

        [Fact]
        public void MonitorForce_ORsIntoTheEnvironmentsBlock()
        {
            var wifi = new FakeNetworkInterface("en0");
            var wired = new FakeNetworkInterface("en12");

            var envBlocked = DynamicNetworkObserver.ResolveEnvironmentBlocks(
                [wifi, wired], [(new InterfaceMatcher("en0"), false)]);

            var desired = DynamicNetworkObserver.ComputeDesiredBlocks([wifi, wired], envBlocked,
                [(wired, [(new InterfaceMatcher("en0"), true)])], NullLogger.Instance);

            Assert.True(desired[wifi]); // false (environment) OR true (monitor)
        }

        [Fact]
        public void MonitorBlocks_NeverMatchTheMonitorsOwnInterface()
        {
            var wired = new FakeNetworkInterface("en12");

            var desired = DynamicNetworkObserver.ComputeDesiredBlocks([wired],
                new Dictionary<INetworkInterface, bool>(),
                [(wired, [(new InterfaceMatcher("en12"), false)])], // a greedy pattern hits its own interface
                NullLogger.Instance);

            Assert.Empty(desired);
        }

        [Fact]
        public void DisabledInterface_KeepsItsBlock()
        {
            // a blocked interface reads as Down (a disable is indistinguishable from a mere
            // disconnect at the status level) — the desired set must keep containing it
            // regardless of status, or the block would flap on every re-plan
            var disabled = new FakeNetworkInterface("en0") { Status = OperationalStatus.Down };

            var envBlocked = DynamicNetworkObserver.ResolveEnvironmentBlocks(
                [disabled], [(new InterfaceMatcher("en0"), false)]);

            var desired = DynamicNetworkObserver.ComputeDesiredBlocks([disabled], envBlocked, [], NullLogger.Instance);

            Assert.True(desired.ContainsKey(disabled));
        }

        [Fact]
        public void LiveMonitorsInterface_NeverEntersTheDesiredSet()
        {
            // the defunct-retry divergence: config A (on if1, blocking if2) fails to start,
            // so B runs on if2 after all — but C already started on if3 in an earlier
            // iteration of the same round, and B's blocks match it; the desired set must
            // spare the interface of ANY live monitor, or the block would kill C's capture
            // under inhibition
            var if2 = new FakeNetworkInterface("if2");
            var if3 = new FakeNetworkInterface("if3");
            var if4 = new FakeNetworkInterface("if4"); // matched by B's blocks, carries no monitor

            var desired = DynamicNetworkObserver.ComputeDesiredBlocks([if2, if3, if4],
                new Dictionary<INetworkInterface, bool>(),
                [(if2, [(new InterfaceMatcher("if3|if4"), false)]), (if3, [])],
                NullLogger.Instance);

            Assert.Equal(["if4"], desired.Keys.Select(i => i.Identity.Id)); // if3 carries C — spared
        }
    }
}
