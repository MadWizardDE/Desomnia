using MadWizard.Desomnia.Configuration.Binding;
using NLog;
using System.Xml.Linq;

namespace MadWizard.Desomnia.Environments
{
    /// <summary>
    /// Merges the &lt;SystemMonitor&gt; contents of all active environment blocks
    /// (in document order) into one effective configuration. Elements are identified
    /// by their name plus their "name" attribute; nameless collection items (as derived
    /// from the modules' config types) are distinct instances and are appended instead
    /// of merged.
    ///
    /// Conflicting values are decided by the blocks' priority - higher supersedes,
    /// regardless of document order. Between EQUAL priorities the onConflict setting
    /// applies: the later block wins (default), the earlier keeps its value, or the
    /// conflict aborts startup. Every merged value is annotated with its origin
    /// (block priority + name), since annotations are what makes this decidable
    /// after the fold has mixed several blocks into one tree.
    /// </summary>
    internal static class ConfigMerger
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        const string NAME_ATTRIBUTE = "name";

        /// <summary>Provenance of a merged value: which environment set it, at which priority.</summary>
        private sealed record MergeOrigin(int Priority, string Environment);

        public static XElement Merge(IEnumerable<EnvironmentBlock> blocks, IReadOnlySet<string> collectionElements, ConflictResolution onConflict)
        {
            XElement? result = null;

            foreach (var block in blocks)
            {
                var origin = new MergeOrigin(block.Priority, block.DisplayName);

                if (result is null)
                    result = Annotate(new XElement(block.Content), origin);
                else
                    MergeElement(result, block.Content, origin, collectionElements, onConflict);
            }

            return result ?? new XElement(EnvironmentParser.SYSTEM_MONITOR_ELEMENT);
        }

        private static void MergeElement(XElement target, XElement source, MergeOrigin origin, IReadOnlySet<string> collectionElements, ConflictResolution onConflict)
        {
            MergeAttributes(target, source, origin, onConflict);

            MergeText(target, source, origin, onConflict);

            foreach (var child in source.Elements())
            {
                var childName = child.Name.LocalName;
                var childItemName = ItemName(child);

                // nameless collection items are distinct instances - never merged
                if (childItemName is null && collectionElements.Contains(childName))
                {
                    target.Add(Annotate(new XElement(child), origin));
                    continue;
                }

                var match = target.Elements().FirstOrDefault(element =>
                    element.Name.LocalName.Equals(childName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ItemName(element), childItemName, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                    target.Add(Annotate(new XElement(child), origin));
                else
                    MergeElement(match, child, origin, collectionElements, onConflict);
            }
        }

        private static void MergeAttributes(XElement target, XElement source, MergeOrigin origin, ConflictResolution onConflict)
        {
            foreach (var attribute in source.Attributes())
            {
                if (attribute.Name.LocalName.Equals(NAME_ATTRIBUTE, StringComparison.OrdinalIgnoreCase))
                    continue; // part of the element's identity

                var existing = target.Attributes().FirstOrDefault(a =>
                    a.Name.LocalName.Equals(attribute.Name.LocalName, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    var added = new XAttribute(attribute);
                    added.AddAnnotation(origin);

                    target.Add(added);
                }
                else if (existing.Value == attribute.Value)
                {
                    // no conflict - let the highest priority that asserted the value back it
                    if (OriginOf(existing).Priority < origin.Priority)
                        Reannotate(existing, origin);
                }
                else if (Resolve(existing, origin, onConflict,
                    $"<{Describe(target)}>: attribute '{attribute.Name.LocalName}'", existing.Value, attribute.Value))
                {
                    existing.Value = attribute.Value;

                    Reannotate(existing, origin);
                }
            }
        }

        private static void MergeText(XElement target, XElement source, MergeOrigin origin, ConflictResolution onConflict)
        {
            var sourceText = source.Nodes().OfType<XText>().Where(text => !string.IsNullOrWhiteSpace(text.Value)).ToList();

            if (sourceText.Count == 0)
                return;

            var targetText = target.Nodes().OfType<XText>().Where(text => !string.IsNullOrWhiteSpace(text.Value)).ToList();

            if (targetText.Count > 0)
            {
                string oldValue = string.Concat(targetText.Select(text => text.Value)).Trim();
                string newValue = string.Concat(sourceText.Select(text => text.Value)).Trim();

                if (oldValue == newValue)
                {
                    if (OriginOf(target).Priority < origin.Priority)
                        Reannotate(target, origin);

                    return; // no conflict - keep the existing nodes
                }

                // the element's own annotation tracks the origin of its text content
                if (!Resolve(target, origin, onConflict, $"<{Describe(target)}>: text content", oldValue, newValue))
                    return;

                targetText.ForEach(text => text.Remove());
            }

            foreach (var text in sourceText)
                target.Add(new XText(text));

            Reannotate(target, origin);
        }

        /// <summary>Decides a value conflict: higher priority always wins; equal priorities resolve per onConflict.</summary>
        private static bool Resolve(XObject existing, MergeOrigin origin, ConflictResolution onConflict, string subject, string oldValue, string newValue)
        {
            var current = OriginOf(existing);

            if (origin.Priority > current.Priority)
            {
                Logger.Debug($"{subject} superseded by higher-priority environment '{origin.Environment}' ('{oldValue}' -> '{newValue}')");

                return true;
            }

            if (origin.Priority < current.Priority)
            {
                Logger.Debug($"{subject} keeps '{oldValue}' from higher-priority environment '{current.Environment}'; " +
                    $"ignoring '{newValue}' from '{origin.Environment}'");

                return false;
            }

            switch (onConflict)
            {
                case ConflictResolution.Last:
                    Logger.Warn($"{subject} overridden by environment '{origin.Environment}' ('{oldValue}' -> '{newValue}')");

                    return true;

                case ConflictResolution.First:
                    Logger.Warn($"{subject} keeps '{oldValue}' from environment '{current.Environment}'; " +
                        $"ignoring '{newValue}' from '{origin.Environment}'");

                    return false;

                default:
                    throw new ConfigurationValueException($"{subject} has conflicting values from environments " +
                        $"'{current.Environment}' ('{oldValue}') and '{origin.Environment}' ('{newValue}') with equal priority. " +
                        $"Set different priorities or change {EnvironmentParser.ONCONFLICT_ATTRIBUTE}.");
            }
        }

        /// <summary>Stamps the whole subtree with its origin (annotations are not copied when an XElement is cloned).</summary>
        private static XElement Annotate(XElement root, MergeOrigin origin)
        {
            foreach (var element in root.DescendantsAndSelf())
            {
                element.AddAnnotation(origin);

                foreach (var attribute in element.Attributes())
                    attribute.AddAnnotation(origin);
            }

            return root;
        }

        private static void Reannotate(XObject node, MergeOrigin origin)
        {
            node.RemoveAnnotations<MergeOrigin>();
            node.AddAnnotation(origin);
        }

        private static MergeOrigin OriginOf(XObject node)
            => node.Annotation<MergeOrigin>() ?? new MergeOrigin(0, "?");

        private static string? ItemName(XElement element)
            => element.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(NAME_ATTRIBUTE, StringComparison.OrdinalIgnoreCase))?.Value;

        private static string Describe(XElement element)
            => ItemName(element) is string name ? $"{element.Name.LocalName} name={name}" : element.Name.LocalName;
    }
}
