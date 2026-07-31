namespace MadWizard.Desomnia.Network.FRITZ.Actions
{
    /// <summary>
    /// Parses a <c>fritz://</c> action string into its parts:
    /// <code>fritz://&lt;box&gt;/&lt;resource&gt;/&lt;id&gt;?&lt;prop&gt;=&lt;value&gt;&amp;…</code>
    /// e.g. <c>fritz://heimdail/ports/eth0?maxspeed=1000</c> →
    /// box <c>heimdail</c>, resource <c>ports</c>, id <c>eth0</c>, { maxspeed = 1000 }.
    ///
    /// <para>This is the plugin-local reading of the <c>fritz</c> scheme, dispatched through
    /// the generic URL-action pipeline ([URLActionHandler], spec §6.4) —
    /// the meaning of a fritz URL is unchanged from its pre-pipeline days.</para>
    /// </summary>
    internal sealed class FritzActionURL
    {
        public const string Scheme = "fritz";

        public required string Box { get; init; }
        public required string Resource { get; init; }
        public required string Id { get; init; }
        public required IReadOnlyDictionary<string, string> Properties { get; init; }

        public static bool TryParse(Uri uri, out FritzActionURL? result)
        {
            result = null;

            if (!string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
                return false;

            var box = uri.Host;
            if (string.IsNullOrEmpty(box))
                return false;

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length != 2)
                return false; // exactly <resource>/<id> — trailing garbage is a config
                              // error now that fritz strings no longer fall through

            result = new FritzActionURL
            {
                Box = box,
                Resource = segments[0],
                Id = segments[1],
                Properties = ParseQuery(uri.Query),
            };
            return true;
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = Uri.UnescapeDataString(pair[..eq]);
                var val = Uri.UnescapeDataString(pair[(eq + 1)..]);
                result[key] = val;
            }

            return result;
        }

        public override string ToString() => $"fritz://{Box}/{Resource}/{Id}";
    }
}
