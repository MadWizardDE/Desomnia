namespace MadWizard.Desomnia.Network.Naming.Browser.Events
{
    public sealed class ServiceInstanceRemovedEventArgs(ServiceInstanceRemovedReason reason) : EventArgs
    {
        public ServiceInstanceRemovedReason Reason => reason;

        public bool HasExpired => Reason == ServiceInstanceRemovedReason.Expired;
    }

    public enum ServiceInstanceRemovedReason
    {
        /// <summary>The record was withdrawn explicitly (an mDNS goodbye, TTL = 0).</summary>
        Goodbye,
        /// <summary>The record lapsed because it was not refreshed before its TTL ran out.</summary>
        Expired
    }
}
