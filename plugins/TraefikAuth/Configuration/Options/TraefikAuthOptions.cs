namespace MadWizard.Desomnia.Network.Traefik.Configuration.Options
{
    internal class TraefikAuthOptions
    {
        public string Prefix { get; set; } = string.Empty; // TODO warnung
        public TimeSpan Timeout { get; set; }
    }
}
