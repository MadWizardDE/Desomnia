namespace MadWizard.Desomnia.Network.SleepProxy
{
    /// <summary>
    /// The four-part ranking metric a Sleep Proxy advertises at the start of its DNS-SD instance label
    /// ("AA-BB-CC-DD", from Apple's mDNSResponder). Clients prefer the proxy with the <em>lowest</em>
    /// metric, compared field by field.
    /// </summary>
    internal readonly struct SleepProxyMetrics
    {
        /// <summary>Device class — dedicated proxy hardware is low, incidental software is high.</summary>
        public byte Type            { get; init; }

        /// <summary>Portability — battery powered scores higher (worse) than mains powered.</summary>
        public byte Portability     { get; init; }

        /// <summary>Additional power needed to act as a proxy.</summary>
        public byte MarginalPower   { get; init; }

        /// <summary>Overall power draw of the host.</summary>
        public byte TotalPower      { get; init; }

        /// <summary>Builds the metric label, e.g. "90-40-70-70".</summary>
        public override string ToString() => $"{Type}-{Portability}-{MarginalPower}-{TotalPower}";
    }
}
