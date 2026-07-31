using MadWizard.Desomnia.Configuration.Binding;
using MadWizard.Desomnia.Environments;
using System.Xml.Linq;
using Xunit;

namespace MadWizard.Desomnia.Tests
{
    public class ConfigMergerTests
    {
        private static readonly IReadOnlySet<string> CollectionElements
            = new HashSet<string>(["NetworkMonitor", "RemoteHost", "Process"], StringComparer.OrdinalIgnoreCase);

        private static EnvironmentBlock Block(string name, string xml, int priority = 0) => new()
        {
            DisplayName = name,
            Priority = priority,
            ConditionAttributes = [],
            Content = XElement.Parse(xml),
        };

        private static XElement Merge(params EnvironmentBlock[] blocks)
            => ConfigMerger.Merge(blocks, CollectionElements, ConflictResolution.Last);

        private static XElement Merge(ConflictResolution onConflict, params EnvironmentBlock[] blocks)
            => ConfigMerger.Merge(blocks, CollectionElements, onConflict);

        [Fact]
        public void LaterBlockWins_OnConflictingAttributes()
        {
            var result = Merge(
                Block("a", """<SystemMonitor onDemand="sleepless" timeout="2min" />"""),
                Block("b", """<SystemMonitor onDemand="wake" />"""));

            Assert.Equal("wake", result.Attribute("onDemand")?.Value);
            Assert.Equal("2min", result.Attribute("timeout")?.Value); // untouched
        }

        [Fact]
        public void NamedCollectionItems_AreMergedByName()
        {
            var result = Merge(
                Block("a", """<SystemMonitor><NetworkMonitor name="WiFi" interface="en0" /></SystemMonitor>"""),
                Block("b", """<SystemMonitor><NetworkMonitor name="WiFi" network="10.0.0.0/24" /><NetworkMonitor name="Ethernet" /></SystemMonitor>"""));

            var monitors = result.Elements("NetworkMonitor").ToList();

            Assert.Equal(2, monitors.Count);
            Assert.Equal("en0", monitors[0].Attribute("interface")?.Value);
            Assert.Equal("10.0.0.0/24", monitors[0].Attribute("network")?.Value);
            Assert.Equal("Ethernet", monitors[1].Attribute("name")?.Value);
        }

        [Fact]
        public void NamelessCollectionItems_AreAppended()
        {
            var result = Merge(
                Block("a", """<SystemMonitor><NetworkMonitor interface="en0" /></SystemMonitor>"""),
                Block("b", """<SystemMonitor><NetworkMonitor interface="en12" /></SystemMonitor>"""));

            var monitors = result.Elements("NetworkMonitor").ToList();

            Assert.Equal(2, monitors.Count);
            Assert.Equal("en0", monitors[0].Attribute("interface")?.Value); // document order preserved
            Assert.Equal("en12", monitors[1].Attribute("interface")?.Value);
        }

        [Fact]
        public void NamelessSingletons_AreMerged()
        {
            var result = Merge(
                Block("a", """<SystemMonitor><DisplayMonitor preventIdle="true" /></SystemMonitor>"""),
                Block("b", """<SystemMonitor><DisplayMonitor disabled="true" /></SystemMonitor>"""));

            var display = Assert.Single(result.Elements("DisplayMonitor"));

            Assert.Equal("true", display.Attribute("preventIdle")?.Value);
            Assert.Equal("true", display.Attribute("disabled")?.Value);
        }

        [Fact]
        public void MergesRecursively()
        {
            var result = Merge(
                Block("a", """<SystemMonitor><NetworkMonitor name="WiFi"><RemoteHost name="nas" /></NetworkMonitor></SystemMonitor>"""),
                Block("b", """<SystemMonitor><NetworkMonitor name="WiFi"><RemoteHost name="nas" onServiceDemand="knock" /><RemoteHost name="pc" /></NetworkMonitor></SystemMonitor>"""));

            var hosts = result.Element("NetworkMonitor")!.Elements("RemoteHost").ToList();

            Assert.Equal(2, hosts.Count);
            Assert.Equal("knock", hosts[0].Attribute("onServiceDemand")?.Value);
            Assert.Equal("pc", hosts[1].Attribute("name")?.Value);
        }

        [Fact]
        public void TextContent_LaterBlockWins()
        {
            var result = Merge(
                Block("a", """<SystemMonitor><Process name="p">/old/path</Process></SystemMonitor>"""),
                Block("b", """<SystemMonitor><Process name="p">/new/path</Process></SystemMonitor>"""));

            Assert.Equal("/new/path", Assert.Single(result.Elements("Process")).Value.Trim());
        }

        [Fact]
        public void HigherPriority_Supersedes_RegardlessOfOrder()
        {
            // an earlier higher-priority block keeps its value against later blocks
            var result = Merge(
                Block("a", """<SystemMonitor onDemand="sleepless" />""", priority: 1),
                Block("b", """<SystemMonitor onDemand="wake" />"""));

            Assert.Equal("sleepless", result.Attribute("onDemand")?.Value);

            // and a later higher-priority block overrides an earlier one
            result = Merge(
                Block("a", """<SystemMonitor onDemand="sleepless" />"""),
                Block("b", """<SystemMonitor onDemand="wake" />""", priority: 1));

            Assert.Equal("wake", result.Attribute("onDemand")?.Value);
        }

        [Fact]
        public void HigherPriority_SupersedesInNestedElements()
        {
            var result = Merge(
                Block("a", """<SystemMonitor><NetworkMonitor name="WiFi" network="10.1.0.0/16" /></SystemMonitor>""", priority: 2),
                Block("b", """<SystemMonitor><NetworkMonitor name="WiFi" network="10.2.0.0/16" interface="en0" /></SystemMonitor>"""));

            var monitor = Assert.Single(result.Elements("NetworkMonitor"));

            Assert.Equal("10.1.0.0/16", monitor.Attribute("network")?.Value); // kept from the higher-priority block
            Assert.Equal("en0", monitor.Attribute("interface")?.Value);       // non-conflicting values still merge
        }

        [Fact]
        public void OnConflictFirst_KeepsTheEarlierValue()
        {
            var result = Merge(ConflictResolution.First,
                Block("a", """<SystemMonitor onDemand="sleepless" />"""),
                Block("b", """<SystemMonitor onDemand="wake" />"""));

            Assert.Equal("sleepless", result.Attribute("onDemand")?.Value);
        }

        [Fact]
        public void OnConflictError_ThrowsOnEqualPriorityConflicts()
        {
            Assert.Throws<ConfigurationValueException>(() => Merge(ConflictResolution.Error,
                Block("a", """<SystemMonitor onDemand="sleepless" />"""),
                Block("b", """<SystemMonitor onDemand="wake" />""")));
        }

        [Fact]
        public void OnConflictError_AcceptsConflictsResolvedByPriority()
        {
            var result = Merge(ConflictResolution.Error,
                Block("a", """<SystemMonitor onDemand="sleepless" />"""),
                Block("b", """<SystemMonitor onDemand="wake" />""", priority: 1));

            Assert.Equal("wake", result.Attribute("onDemand")?.Value);
        }

        [Fact]
        public void OnConflictError_AcceptsIdenticalValues()
        {
            var result = Merge(ConflictResolution.Error,
                Block("a", """<SystemMonitor onDemand="sleepless" />"""),
                Block("b", """<SystemMonitor onDemand="sleepless" />"""));

            Assert.Equal("sleepless", result.Attribute("onDemand")?.Value);
        }

        [Fact]
        public void NoActiveBlocks_YieldsBareSystemMonitor()
        {
            var result = Merge();

            Assert.Equal("SystemMonitor", result.Name.LocalName);
            Assert.False(result.HasAttributes);
            Assert.Empty(result.Elements());
        }
    }
}
