using MadWizard.Desomnia.Network.Bridges;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Manager;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using ConfiguredNetwork = (MadWizard.Desomnia.Network.Configuration.NetworkMonitorConfig Config,
    MadWizard.Desomnia.Network.Bridges.InterfaceMatcher Matcher,
    System.Collections.Generic.IReadOnlyList<(MadWizard.Desomnia.Network.Bridges.InterfaceMatcher Matcher, bool Force)> Blocks);
using NetworkBlocker = (MadWizard.Desomnia.Network.Configuration.NetworkMonitorConfig Config,
    MadWizard.Desomnia.Network.Manager.INetworkInterface Interface);

namespace MadWizard.Desomnia.Network.Tests
{
    /// <summary>
    /// The planning half of the monitor-scoped NetworkInterfaceBlock: which interfaces the
    /// DynamicNetworkObserver must not monitor because another candidate blocks them.
    /// </summary>
    public class BlockedInterfaceResolutionTests
    {
        // the macOS scenario: the WiFi ("alpha") enumerates before the wired adapter ("bravo"),
        // but the wired configuration blocks it
        [Fact]
        public void EnumerationOrder_DoesNotDecide()
        {
            var wifi = new FakeNetworkInterface("alpha");
            var wired = new FakeNetworkInterface("bravo");

            var wifiConfig = Entry("wifi");
            var wiredConfig = Entry("wired", blocks: "alpha");

            var blocked = Resolve([wifiConfig, wiredConfig], [(wifi, [wifiConfig]), (wired, [wiredConfig])]);

            var blocker = Assert.Single(blocked);

            Assert.Same(wifi, blocker.Key);
            Assert.Same(wired, blocker.Value.Interface);
            Assert.Same(wiredConfig.Config, blocker.Value.Config);
        }

        [Fact]
        public void BlockedCandidate_BlocksNothingItself()
        {
            var a = new FakeNetworkInterface("alpha");
            var b = new FakeNetworkInterface("bravo");
            var c = new FakeNetworkInterface("charlie");

            var first = Entry("first", blocks: "bravo");    // blocks b...
            var second = Entry("second", blocks: "charlie"); // ...so its block on c is void

            var blocked = Resolve([first, second], [(a, [first]), (b, [second]), (c, [Entry("third")])]);

            Assert.Equal(["bravo"], Ids(blocked));
        }

        [Fact]
        public void MutualBlock_EarlierConfigWins()
        {
            var a = new FakeNetworkInterface("alpha");
            var b = new FakeNetworkInterface("bravo");

            var first = Entry("first", blocks: "bravo");
            var second = Entry("second", blocks: "alpha");

            var blocked = Resolve([first, second], [(a, [first]), (b, [second])]);

            Assert.Equal(["bravo"], Ids(blocked)); // and never both

            // ...regardless of which interface enumerates first
            blocked = Resolve([first, second], [(b, [second]), (a, [first])]);

            Assert.Equal(["bravo"], Ids(blocked));
        }

        [Fact]
        public void ReverseChain_ResolvesThroughDefunctRounds()
        {
            // "first" (on bravo) blocks charlie, "second" (on alpha) blocks bravo: the first
            // resolution over-blocks charlie, because bravo's fate is only known once the
            // start loop ran - the observer then reports bravo's blocker defunct and charlie
            // gets its monitor in the next round
            var a = new FakeNetworkInterface("alpha");
            var b = new FakeNetworkInterface("bravo");
            var c = new FakeNetworkInterface("charlie");

            var first = Entry("first", blocks: "charlie");
            var second = Entry("second", blocks: "bravo");
            var third = Entry("third");

            List<(INetworkInterface, List<ConfiguredNetwork>)> candidates = [(a, [second]), (b, [first]), (c, [third])];

            var blocked = Resolve([first, second], candidates);

            Assert.Equal(["bravo", "charlie"], Ids(blocked).Order());

            blocked = Resolve([first, second], candidates, defunct: new HashSet<NetworkBlocker> { (first.Config, b) });

            Assert.Equal(["bravo"], Ids(blocked));
        }

        [Fact]
        public void OwnInterface_IsNeverBlocked()
        {
            var a = new FakeNetworkInterface("alpha");
            var b = new FakeNetworkInterface("bravo");

            var config = Entry("greedy", blocks: "alpha|bravo"); // matches its own interface too

            var blocked = Resolve([config], [(a, [config]), (b, [Entry("other")])]);

            Assert.Equal(["bravo"], Ids(blocked));
        }

        [Fact]
        public void SecondaryConfig_DoesNotBlock()
        {
            var a = new FakeNetworkInterface("alpha");
            var b = new FakeNetworkInterface("bravo");

            var primary = Entry("primary");
            var secondary = Entry("secondary", blocks: "bravo"); // matches a too, but second in line

            var blocked = Resolve([primary, secondary], [(a, [primary, secondary]), (b, [Entry("other")])]);

            Assert.Empty(blocked); // only the config about to run may block
        }

        [Fact]
        public void EveryBlockOfAConfig_Acts()
        {
            var a = new FakeNetworkInterface("alpha");
            var b = new FakeNetworkInterface("bravo");
            var c = new FakeNetworkInterface("charlie");

            var config = Entry("multi", blocks: ["bravo", "charlie"]);

            var blocked = Resolve([config], [(a, [config]), (b, [Entry("b")]), (c, [Entry("c")])]);

            Assert.Equal(["bravo", "charlie"], Ids(blocked).Order());
        }

        private static ConfiguredNetwork Entry(string name, string? blocks = null)
            => Entry(name, blocks is null ? [] : [blocks]);

        private static ConfiguredNetwork Entry(string name, string[] blocks)
        {
            IReadOnlyList<(InterfaceMatcher Matcher, bool Force)> blockMatchers =
                [.. blocks.Select(pattern => (new InterfaceMatcher(pattern), false))];

            return (new NetworkMonitorConfig { Name = name }, new InterfaceMatcher(), blockMatchers);
        }

        private static IEnumerable<string> Ids(Dictionary<INetworkInterface, NetworkBlocker> blocked)
            => blocked.Keys.Select(@interface => @interface.Identity.Id);

        private static Dictionary<INetworkInterface, NetworkBlocker> Resolve(
            IEnumerable<ConfiguredNetwork> matchers,
            List<(INetworkInterface Interface, List<ConfiguredNetwork> Matching)> candidates,
            IReadOnlySet<NetworkBlocker>? defunct = null)
        {
            return DynamicNetworkObserver.ResolveBlockedInterfaces(matchers, candidates,
                defunct ?? new HashSet<NetworkBlocker>(), NullLogger.Instance);
        }
    }
}
