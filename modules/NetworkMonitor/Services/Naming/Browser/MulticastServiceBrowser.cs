using ConcurrentCollections;
using MadWizard.Desomnia.Network.Naming.Browser.Events;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Naming
{
    /// <summary>
    /// A DNS-SD service browser layered on top of <see cref="MulticastDNSService"/>. It turns the raw,
    /// possibly fragmented record flow into <see cref="ServiceInstance"/> objects -- resolving an instance
    /// across several queries when records are missing -- and keeps them live for as long as the
    /// application holds a reference, re-querying before their TTLs lapse and retiring them on goodbye or
    /// timeout. Discovery of <em>new</em> instances is driven by active <see cref="ServiceBrowseRequest"/>s;
    /// maintenance of existing ones is driven purely by reference liveness (weak references).
    /// </summary>
    public sealed class MulticastServiceBrowser : IMulticastDNSListener, IDisposable
    {
        private static readonly TimeSpan    MaintenanceInterval         = TimeSpan.FromSeconds(5);
        private static readonly int         MaintenanceErrorThreshold   = 5;
        private static readonly double      RefreshThreshold            = 0.8;
        /// <summary>A maintenance tick later than this since the previous one signals a resume from suspend.</summary>
        private static readonly double      ReanchorThreshold           = 3;

        public required ILogger<MulticastServiceBrowser> Logger { private get; init; }

        public required NetworkSegment      Network         { private get; init; }
        public required MulticastDNSService MulticastDNS    { private get; init; }

        readonly ConcurrentDictionary<DomainName, WeakReference<ServiceInstance>> _instances = [];

        readonly ConcurrentHashSet<ServiceBrowseRequest> _requests = [];

        readonly CancellationTokenSource _cancellation = new();

        Task? _maintenance;

        public ServiceBrowseRequest EnumerateInstances(DomainName serviceDomainName, CancellationToken cancellation = default)
        {
            var request = new ServiceBrowseRequest(serviceDomainName, cancellation);

            request.Completed += (sender, args) => _requests.TryRemove(request);

            lock (_requests)
            {
                _maintenance ??= Task.Run(() => MaintainAsync(_cancellation.Token));

                _requests.Add(request);
            }

            // Surface everything we already know about, then keep streaming new arrivals.
            foreach (ServiceInstance instance in LiveInstances(request.ServiceDomainName))
                request.Enqueue(instance);

            MulticastDNS.Browse(request.ServiceDomainName, DnsType.PTR);

            return request;
        }

        void IMulticastDNSListener.ProcessResponse(Message message)
        {
            if (_requests.IsEmpty && _instances.IsEmpty)
                return;

            IEnumerable<ResourceRecord> records = message.Answers.Concat(message.AdditionalRecords);

            // SRV -- the identity (target + port) of an instance we browse for or already track.
            var newInstances = new List<ServiceInstance>();
            foreach (SRVRecord srv in records.OfType<SRVRecord>())
            {
                if (!TryGetInstance(srv.Name, out ServiceInstance? existing))
                {
                    if (srv.TTL > TimeSpan.Zero && IsServiceRequested(srv.ServiceDomainName))
                    {
                        var instance = new ServiceInstance(srv);

                        _instances[srv.Name] = new WeakReference<ServiceInstance>(instance);

                        newInstances.Add(instance);
                    }
                }
                else if (srv.TTL == TimeSpan.Zero) // goodbye
                {
                    Remove(existing, ServiceInstanceRemovedReason.Goodbye);
                }
                else
                {
                    existing.TTL = srv.TTL;
                }
            }

            // Address records -- enrich, renew or retire the addresses of resolved instances.
            foreach (AddressRecord adr in records.OfType<AddressRecord>())
            {
                foreach (ServiceInstance instance in LiveInstances().Where(i => i.HostDomainName == adr.Name))
                {
                    if (adr.TTL == TimeSpan.Zero)
                        instance.RemoveAddress(adr.Address, ServiceInstanceRemovedReason.Goodbye);
                    else
                        instance.AddAddress(adr.Address, adr.TTL);
                }
            }

            foreach (TXTRecord txt in records.OfType<TXTRecord>())
            {
                foreach (ServiceInstance instance in LiveInstances().Where(i => i.DomainName == txt.Name))
                {
                    instance.Text = [.. txt.Strings]; // set or replace all strings
                }
            }

            // Newly resolved instances -- chase required addresses that weren't bundled, then surface them.
            foreach (ServiceInstance instance in newInstances)
            {
                if (instance.Text == null)
                    MulticastDNS.Browse(instance.HostDomainName, DnsType.TXT);

                if (!instance.Addresses.Any(ip => ip.AddressFamily == AddressFamily.InterNetwork))
                    MulticastDNS.Browse(instance.HostDomainName, DnsType.A);
                if (!instance.Addresses.Any(ip => ip.AddressFamily == AddressFamily.InterNetworkV6))
                    MulticastDNS.Browse(instance.HostDomainName, DnsType.AAAA);

                PublishToSubscribers(instance);
            }

            // PTR -- a service-type enumeration answer; chase the SRV if we don't have the instance yet.
            foreach (PTRRecord ptr in records.OfType<PTRRecord>())
            {
                if (!IsServiceRequested(ptr.Name))
                    continue;

                if (TryGetInstance(ptr.DomainName, out ServiceInstance? existing))
                {
                    if (ptr.TTL == TimeSpan.Zero) // goodbye for the whole instance
                    {
                        Remove(existing, ServiceInstanceRemovedReason.Goodbye);

                        continue;
                    }
                }
                else if (ptr.TTL > TimeSpan.Zero)
                {
                    MulticastDNS.Browse(ptr.DomainName, DnsType.SRV);
                }
            }
        }

        private async Task MaintainAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(MaintenanceInterval);

            DateTime lastRun = DateTime.Now;

            bool ShouldDoMaintenance() => !_instances.IsEmpty || _requests.Count > 0;

            // A tick that lands far later than its interval means the machine was suspended
            // (the process was frozen) and we could not refresh -- re-confirm, don't prune.
            bool HasSkippedMaintenance() => DateTime.Now - lastRun > MaintenanceInterval * ReanchorThreshold;

            try
            {
                int errors = 0;

                while (ShouldDoMaintenance() && await timer.WaitForNextTickAsync(token)) using (Network.Mutex.Lock(token))
                {
                    try
                    {
                        if (HasSkippedMaintenance())
                            ReanchorInstances();
                        else
                            RefreshInstances();

                        RequeryRequests();

                        errors = 0;
                    }
                    catch (Exception ex)
                    {
                        if (errors++ > MaintenanceErrorThreshold)
                            throw;

                        Logger.LogWarning(ex, "Maintenance skipped");
                    }
                    finally
                    {
                        lastRun = DateTime.Now;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // browser disposed
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Maintenance stopped");
            }
            finally
            {
                _maintenance = null;
            }
        }

        private void ReanchorInstances()
        {
            foreach (var instance in LiveInstances())
            {
                // The machine was suspended past the TTLs without any chance to refresh, so every
                // record now looks "expired" purely because wall-clock jumped. Re-anchor the lifetimes
                // and re-confirm on the network rather than pruning records that may still be valid;
                // genuinely gone ones simply won't answer and lapse a TTL later.
                instance.ResetTTL();

                MulticastDNS.Browse(instance.DomainName, DnsType.SRV);
                MulticastDNS.Browse(instance.HostDomainName, DnsType.A);
                MulticastDNS.Browse(instance.HostDomainName, DnsType.AAAA);
            }
        }

        private void RefreshInstances()
        {
            foreach (var instance in LiveInstances())
            {
                if (instance.HasExpired)
                {
                    Remove(instance, ServiceInstanceRemovedReason.Expired);

                    continue;
                }

                foreach (IPAddress ip in instance.Addresses.ToArray())
                {
                    if (instance[ip].HasExpired)
                    {
                        instance.RemoveAddress(ip, ServiceInstanceRemovedReason.Expired);
                    }
                    else if (instance[ip].ElapsedTTL > RefreshThreshold)
                    {
                        switch (ip.AddressFamily)
                        {
                            case AddressFamily.InterNetwork:
                                MulticastDNS.Browse(instance.HostDomainName, DnsType.A);
                                break;
                            case AddressFamily.InterNetworkV6:
                                MulticastDNS.Browse(instance.HostDomainName, DnsType.AAAA);
                                break;
                        }
                    }
                }

                if (instance.ElapsedTTL > RefreshThreshold)
                {
                    MulticastDNS.Browse(instance.DomainName, DnsType.SRV);
                }
            }
        }

        private void RequeryRequests()
        {
            DateTime now = DateTime.Now;

            // RFC 6762 §5.2 -- periodically re-issue each browse to pick up freshly started (or
            // missed) servers. The proxies we already know ride along as known answers, so
            // unchanged responders stay silent and the link stays quiet (§7.1).
            foreach (ServiceBrowseRequest request in _requests)
            {
                if (request.ShouldRequery(now))
                {
                    IEnumerable<ResourceRecord> KnownInstances()
                    {
                        foreach (ServiceInstance instance in LiveInstances(request.ServiceDomainName))
                            yield return new PTRRecord
                            {
                                Name = request.ServiceDomainName,
                                DomainName = instance.DomainName,

                                TTL = instance.Expires - now,
                            };
                    }

                    MulticastDNS.Browse(request.ServiceDomainName, DnsType.PTR, KnownInstances());
                }
            }
        }

        private bool IsServiceRequested(DomainName serviceDomainName) => _requests.Any(r => r.HasRequested(serviceDomainName));

        private void PublishToSubscribers(ServiceInstance instance)
        {
            foreach (ServiceBrowseRequest request in _requests.Where(r => r.HasRequested(instance.ServiceDomainName)))
            {
                request.Enqueue(instance);
            }
        }

        private void Remove(ServiceInstance instance, ServiceInstanceRemovedReason reason)
        {
            if (_instances.TryRemove(instance.DomainName, out _))
            {
                instance.TriggerRemoved(reason);
            }
        }

        private bool TryGetInstance(DomainName name, [NotNullWhen(true)] out ServiceInstance? instance)
        {
            instance = null;

            return _instances.TryGetValue(name, out WeakReference<ServiceInstance>? weak) && weak.TryGetTarget(out instance);
        }

        private IEnumerable<ServiceInstance> LiveInstances(DomainName? serviceDomainName = null)
        {
            foreach ((DomainName name, WeakReference<ServiceInstance> weak) in _instances.ToArray())
            {
                if (weak.TryGetTarget(out ServiceInstance? instance))
                {
                    if (serviceDomainName == null || instance.ServiceDomainName == serviceDomainName)
                        yield return instance;
                }
                else
                {
                    _instances.Remove(name, out _); // reference dropped elsewhere -- forget it
                }
            }
        }

        public void Dispose()
        {
            foreach (var req in _requests.ToArray())
                req.Dispose();

            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }

    file static class ServiceInstanceEx
    {
        extension (ServiceInstance instance)
        {
            public bool HasExpired => instance.Expires < DateTime.Now;

            public double ElapsedTTL
            {
                get
                {
                    return (DateTime.Now - instance.Expires) / instance.TTL + 1;
                }
            }
        }

        extension (IPAddressOptions options)
        {
            public double ElapsedTTL
            {
                get
                {
                    if (options.Expires is DateTime expires && options.TTL is TimeSpan ttl)
                    {
                        return (DateTime.Now - expires) / ttl + 1;
                    }

                    throw new Exception("ServiceInstance IP without TTL");
                }
            }
        }
    }
}
