using MadWizard.Desomnia.Network.Configuration.Converter;
using System.ComponentModel;

namespace MadWizard.Desomnia.Network.SleepProxy
{
    /// <summary>
    /// The four-part ranking metric a Sleep Proxy advertises at the start of its DNS-SD instance label
    /// ("AA-BB-CC-DD", from Apple's mDNSResponder). Clients prefer the proxy with the <em>lowest</em>
    /// metric, compared field by field.
    /// </summary>
    [TypeConverter(typeof(SleepProxyMetricsConverter))]
    internal readonly struct SleepProxyMetrics : IComparable<SleepProxyMetrics>
    {
        public static SleepProxyMetrics Best    => new(10, 10, 10, 10);
        public static SleepProxyMetrics Average => new(50, 50, 50, 50);
        public static SleepProxyMetrics Worst   => new(99, 99, 99, 99);

        public SleepProxyMetrics(byte intent, byte portability, byte marginalPower, byte totalPower)
        {
            Intent = intent;
            Portability = portability;
            MarginalPower = marginalPower;
            TotalPower = totalPower;
        }

        /// <summary>Device class — dedicated proxy hardware is low, incidental software is high.</summary>
        public byte Intent          { get; init; }
        /// <summary>Portability — battery powered scores higher (worse) than mains powered.</summary>
        public byte Portability     { get; init; }
        /// <summary>Additional power needed to act as a proxy.</summary>
        public byte MarginalPower   { get; init; }
        /// <summary>Overall power draw of the host.</summary>
        public byte TotalPower      { get; init; }

        public int CompareTo(SleepProxyMetrics other)
        {
            int result = Intent.CompareTo(other.Intent);
            if (result != 0) return result;

            result = Portability.CompareTo(other.Portability);
            if (result != 0) return result;

            result = MarginalPower.CompareTo(other.MarginalPower);
            if (result != 0) return result;

            return TotalPower.CompareTo(other.TotalPower);
        }

        /// <summary>Builds the metric label, e.g. "90-40-70-70".</summary>
        public override string ToString() => $"{Intent}-{Portability}-{MarginalPower}-{TotalPower}";
    }

    // Sleep Proxy Server Property Encoding
    //
    // Sleep Proxy Servers are advertised using a structured service name, consisting of four
    // metrics followed by a human-readable name. The metrics assist clients in deciding which
    // Sleep Proxy Server(s) to use when multiple are available on the network. Each metric
    // is a two-digit decimal number in the range 10-99. Lower metrics are generally better.
    //
    //   AA-BB-CC-DD Name
    //
    // Metrics:
    //
    // AA = Intent
    // BB = Portability
    // CC = Marginal Power
    // DD = Total Power
    //
    //
    // ** Intent Metric **
    //
    // 20 = Dedicated Sleep Proxy Server -- a device, permanently powered on,
    //      installed for the express purpose of providing Sleep Proxy Service.
    //
    // 30 = Primary Network Infrastructure Hardware -- a router, DHCP server, NAT gateway,
    //      or similar permanently installed device which is permanently powered on.
    //      This is hardware designed for the express purpose of being network
    //      infrastructure, and for most home users is typically a single point
    //      of failure for the local network -- e.g. most home users only have
    //      a single NAT gateway / DHCP server. Even though in principle the
    //      hardware might technically be capable of running different software,
    //      a typical user is unlikely to do that. e.g. AirPort base station.
    //
    // 40 = Primary Network Infrastructure Software -- a general-purpose computer
    //      (e.g. Mac, Windows, Linux, etc.) which is currently running DHCP server
    //      or NAT gateway software, but the user could choose to turn that off
    //      fairly easily. e.g. iMac running Internet Sharing
    //
    // 50 = Secondary Network Infrastructure Hardware -- like primary infrastructure
    //      hardware, except not a single point of failure for the entire local network.
    //      For example, an AirPort base station in bridge mode. This may have clients
    //      associated with it, and if it goes away those clients will be inconvenienced,
    //      but unlike the NAT gateway / DHCP server, the entire local network is not
    //      dependent on it.
    //
    // 60 = Secondary Network Infrastructure Software -- like 50, but in a general-
    //      purpose CPU.
    //
    // 70 = Incidentally Available Hardware -- a device which has no power switch
    //      and is generally left powered on all the time. Even though it is not a
    //      part of what we conventionally consider network infrastructure (router,
    //      DHCP, NAT, DNS, etc.), and the rest of the network can operate fine
    //      without it, since it's available and unlikely to be turned off, it is a
    //      reasonable candidate for providing Sleep Proxy Service e.g. Apple TV,
    //      or an AirPort base station in client mode, associated with an existing
    //      wireless network (e.g. AirPort Express connected to a music system, or
    //      being used to share a USB printer).
    //
    // 80 = Incidentally Available Software -- a general-purpose computer which
    //      happens at this time to be set to "never sleep", and as such could be
    //      useful as a Sleep Proxy Server, but has not been intentionally provided
    //      for this purpose. Of all the Intent Metric categories this is the
    //      one most likely to be shut down or put to sleep without warning.
    //      However, if nothing else is availalable, it may be better than nothing.
    //      e.g. Office computer in the workplace which has been set to "never sleep"
    //
    //
    // ** Portability Metric **
    //
    // Inversely related to mass of device, on the basis that, all other things
    // being equal, heavier devices are less likely to be moved than lighter devices.
    // E.g. A MacBook running Internet Sharing is probably more likely to be
    // put to sleep and taken away than a Mac Pro running Internet Sharing.
    // The Portability Metric is a logarithmic decibel scale, computed by taking the
    // (approximate) mass of the device in milligrammes, taking the base 10 logarithm
    // of that, multiplying by 10, and subtracting the result from 100:
    //
    //   Portability Metric = 100 - (log10(mg) * 10)
    //
    // The Portability Metric is not necessarily computed literally from the actual
    // mass of the device; the intent is just that lower numbers indicate more
    // permanent devices, and higher numbers indicate devices more likely to be
    // removed from the network, e.g., in order of increasing portability:
    //
    // Mac Pro < iMac < Laptop < iPhone
    //
    // Example values:
    //
    // 10 = 1 metric tonne
    // 40 = 1kg
    // 70 = 1g
    // 90 = 10mg
    //
    //
    // ** Marginal Power and Total Power Metrics **
    //
    // The Marginal Power Metric is the power difference between sleeping and staying awake
    // to be a Sleep Proxy Server.
    //
    // The Total Power Metric is the total power consumption when being Sleep Proxy Server.
    //
    // The Power Metrics use a logarithmic decibel scale, computed as ten times the
    // base 10 logarithm of the (approximate) power in microwatts:
    //
    //   Power Metric = log10(uW) * 10
    //
    // Higher values indicate higher power consumption. Example values:
    //
    // 10 =  10 uW
    // 20 = 100 uW
    // 30 =   1 mW
    // 60 =   1 W
    // 90 =   1 kW
}