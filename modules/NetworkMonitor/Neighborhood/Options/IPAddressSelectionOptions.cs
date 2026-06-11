using System.Net;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Neighborhood.Options
{
    public struct IPAddressSelectionOptions
    {
        public bool SortByHappyEyeballs { get; set; } = true;

        internal IEnumerable<AddressFamily> IncludeFamilies
        {
            private init; readonly get => field ??
            [
                AddressFamily.InterNetwork,
                AddressFamily.InterNetworkV6
            ];
        }

        internal readonly bool Include(IPAddress ip)
        {
            if (!IncludeFamilies.Contains(ip.AddressFamily))
                return false;

            return true;
        }

        public IPAddressSelectionOptions()
        {

        }

        public IPAddressSelectionOptions(params IEnumerable<AddressFamily> families)
        {
            IncludeFamilies = families;
        }

        internal readonly IEnumerable<IPAddress> Select(IEnumerable<IPAddress> addresses)
        {
            addresses = addresses.Where(Include);

            if (SortByHappyEyeballs)
            {
                addresses = InterleaveByFamily(addresses);
            }

            return addresses;
        }

        /// <summary>
        /// Orders addresses per the Happy Eyeballs algorithm (RFC 8305 §4): the address families are
        /// interleaved — the preferred family (IPv6) first — so that connection attempts never exhaust one
        /// family before trying the other.
        /// </summary>
        static internal IEnumerable<IPAddress> InterleaveByFamily(IEnumerable<IPAddress> addresses)
        {
            var families = addresses
                .GroupBy(ip => ip.AddressFamily)
                .OrderBy(group => group.Key == AddressFamily.InterNetworkV6 ? 0 : 1) // IPv6 leads
                .Select(group => new Queue<IPAddress>(group))
                .ToList();

            while (families.Any(queue => queue.Count > 0))
                foreach (var queue in families.Where(queue => queue.Count > 0))
                    yield return queue.Dequeue();
        }
    }
}
