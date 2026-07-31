using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.Extensions.Configuration.Xml
{
    /*
     * Most quirks of the XML format were historically smoothed over here by rewriting the
     * document before the stock provider parsed it (synthetic "__empty"/"text" attributes,
     * enum and TimeSpan value rewriting). These live type-aware in
     * MadWizard.Desomnia.Configuration.Binding.StrictConfigurationBinder now. Only three
     * fixups remain that must happen on the XML level:
     *
     *  1. Value-less attributes ("<traffic must ...>") are not well-formed XML and are
     *     expanded by plain string replacement before parsing.
     *  2. Self-closing empty elements ("<element/>") produce no configuration key at all,
     *     so their presence would be invisible to the binder. Forcing them to serialize
     *     as "<element></element>" makes the provider emit an empty value for them.
     *  3. Nameless collection elements get a synthesized name attribute. This keeps the
     *     provider's key layout deterministic: a single element without a name attribute
     *     would otherwise flatten its attributes directly into the collection section,
     *     making items indistinguishable from attributes. Which element names form
     *     collections is no longer registered by hand, but derived from the modules'
     *     configuration types (see AddCollectionElementsOf).
     */
    public class ExtendedXmlConfigurationSource : XmlConfigurationSource
    {
        internal readonly Dictionary<string, AttributeMapping> BooleanAttributes = [];
        internal readonly HashSet<string> CollectionElements = new(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, CollectionNameBuilder> CollectionNameBuilders = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// When set (by the EnvironmentMonitor), the provider loads this stream instead
        /// of the file contents. Path stays pointed at the file for error reporting.
        /// </summary>
        internal Func<Stream>? EffectiveConfiguration { get; set; }

        public delegate string CollectionNameBuilder(XElement element, uint nr);

        public ExtendedXmlConfigurationSource(string path, bool optional = false, bool reloadOnChange = false)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException($"path = {path}");

            Path = path;
            Optional = optional;
            ReloadOnChange = reloadOnChange;

            ResolveFileProvider();
        }

        public ExtendedXmlConfigurationSource AddBooleanAttribute(string name, AttributeMapping mapping)
        {
            BooleanAttributes.Add(name, mapping);
            return this;
        }

        /// <summary>
        /// Registers an explicit name builder for nameless elements of the given collection.
        /// Use this when code relies on the synthesized name format (which is otherwise an
        /// implementation detail, defaulting to "{elementName}#{nr}").
        /// </summary>
        public ExtendedXmlConfigurationSource AddCollectionNameBuilder(string elementName, CollectionNameBuilder builder)
        {
            CollectionNameBuilders[elementName] = builder;
            CollectionElements.Add(elementName); // an explicit builder also marks the element as a collection

            return this;
        }

        /// <summary>
        /// Walks the given configuration type and records the names of all properties holding
        /// collections of complex items. XML elements with these names are collection elements
        /// and get a synthesized name attribute if they don't carry one.
        /// </summary>
        public ExtendedXmlConfigurationSource AddCollectionElementsOf(Type configType)
        {
            CollectCollectionElements(configType, []);
            return this;
        }

        private void CollectCollectionElements(Type type, HashSet<Type> visited)
        {
            if (IsFrameworkType(type) || !visited.Add(type))
                return;

            // run the type initializer, so custom TypeConverters registered there
            // (e.g. in a static constructor via TypeDescriptor.AddAttributes) take
            // effect before IsComplexType queries them
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);

            for (Type? t = type; t is not null && t != typeof(object); t = t.BaseType)
            {
                const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                foreach (var property in t.GetProperties(declared))
                {
                    if (FindComplexItemType(property.PropertyType) is Type itemType)
                    {
                        CollectionElements.Add(property.Name);

                        CollectCollectionElements(itemType, visited);
                    }
                    else if (IsComplexType(property.PropertyType))
                    {
                        CollectCollectionElements(property.PropertyType, visited);
                    }
                }
            }
        }

        /// <returns>The item type, if the given type is a collection of complex items.</returns>
        private static Type? FindComplexItemType(Type type)
        {
            if (type == typeof(string) || type.IsArray)
                return null;

            IEnumerable<Type> candidates = type.GetInterfaces();
            if (type.IsInterface)
                candidates = candidates.Prepend(type);

            foreach (var candidate in candidates)
            {
                if (candidate.IsConstructedGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    var itemType = candidate.GenericTypeArguments[0];

                    return IsComplexType(itemType) ? itemType : null;
                }
            }

            return null;
        }

        /// <returns>true, if the type binds by its children (attributes/elements) rather than from a string value.</returns>
        private static bool IsComplexType(Type type)
        {
            if (!(type.IsClass || type.IsInterface) || type == typeof(string) || IsFrameworkType(type))
                return false;

            return !TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));
        }

        private static bool IsFrameworkType(Type type)
            => type.Namespace is string ns && (ns == "System" || ns.StartsWith("System.") || ns.StartsWith("Microsoft."));

        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);

            return new CustomXmlConfigurationProvider(this);
        }

        public class AttributeMapping : IIEnumerable<KeyValuePair<string, string>>
        {
            readonly Dictionary<string, string> _mappings = [];

            public string this[string key] { get => _mappings[key]; set { _mappings[key] = value; } }

            IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
            {
                return _mappings.GetEnumerator();
            }
        }
    }

    class CustomXmlConfigurationProvider(ExtendedXmlConfigurationSource source) : XmlConfigurationProvider(source)
    {
        internal const string NAME_ATTRIBUTE_NAME = "name";

        public override void Load()
        {
            if (source.EffectiveConfiguration is { } effective)
                Load(effective());
            else
                base.Load(); // stock file loading
        }

        public override void Load(Stream stream)
        {
            if (source.BooleanAttributes.Count > 0)
                stream = ReplaceBooleanAttributes(stream);

            using MemoryStream memory = new();

            XDocument xml = XDocument.Load(stream);
            TraverseNodes(xml.Root!);
            xml.Save(memory);

            memory.Position = 0;

            base.Load(memory);

            stream.Dispose();
        }

        private Stream ReplaceBooleanAttributes(Stream input)
        {
            // 1. Read Stream into string
            string content;
            using (var reader = new StreamReader(input, Encoding.UTF8, true, 1024, leaveOpen: true))
            {
                input.Position = 0; // Ensure we're at the start
                content = reader.ReadToEnd();
            }

            // 2. Perform replacements
            foreach (var replacement in source.BooleanAttributes)
            {
                var key = " " + replacement.Key;
                var value = " " + string.Join(' ', replacement.Value.Select(attribute => $"{attribute.Key}=\"{attribute.Value}\""));

                // IMPROVE this is a simple string replacement, which may not be safe for all XML content

                content = content.Replace(key, value, StringComparison.InvariantCultureIgnoreCase);
            }

            // 3. Convert string back to Stream
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        private void TraverseNodes(XElement element)
        {
            SupportNamelessCollectionElements(element);

            SupportEmptyNode(element);

            foreach (XElement childElement in element.Elements())
                TraverseNodes(childElement);
        }

        private void SupportNamelessCollectionElements(XElement element)
        {
            Dictionary<string, uint>? counters = null;

            foreach (var child in element.Elements())
            {
                var elementName = child.Name.LocalName;

                if (source.CollectionElements.Contains(elementName))
                {
                    if (child.Attribute(NAME_ATTRIBUTE_NAME) is null)
                    {
                        counters ??= new(StringComparer.OrdinalIgnoreCase);
                        counters.TryGetValue(elementName, out uint nr);
                        counters[elementName] = ++nr;

                        var name = source.CollectionNameBuilders.TryGetValue(elementName, out var builder)
                            ? builder(child, nr)
                            : $"{elementName}#{nr}";

                        child.Add(new XAttribute(NAME_ATTRIBUTE_NAME, name));
                    }
                }
            }
        }

        private static void SupportEmptyNode(XElement element)
        {
            if (!(element.HasAttributes || element.Nodes().Any()))
            {
                element.Add(new XText(string.Empty)); // force "<x></x>", so the element emits an (empty) value
            }
        }
    }
}
