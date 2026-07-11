using System.Reflection;

namespace MadWizard.Desomnia.Network
{
    /// <summary>
    /// The running implementation's identity, advertised e.g. in the sleep-proxy TXT record so that
    /// browsers can tell Desomnia apart from other Bonjour Sleep Proxies. The version is read from
    /// the entry assembly's <see cref="AssemblyInformationalVersionAttribute"/> -- managed metadata
    /// baked in at build time (see the CI notes), which is platform-independent and survives
    /// single-file publishing.
    /// </summary>
    public static class ProductInfo
    {
        /// <summary>The implementation name.</summary>
        public const string Name = "Desomnia";

        /// <summary>The implementation version, e.g. "1.4.2"; "0.0.0" when the build set none.</summary>
        public static string Version { get; } = ResolveVersion();

        private static string ResolveVersion()
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(ProductInfo).Assembly;

            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            // strip any build metadata suffix the SDK appends (e.g. "1.4.2+9c3f0a1")
            var version = informational?.Split('+')[0];

            return string.IsNullOrEmpty(version) ? "0.0.0" : version;
        }
    }
}
