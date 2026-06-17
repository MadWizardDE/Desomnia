using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Context;
using MadWizard.Desomnia.Network.Neighborhood;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network
{
    public class NetworkJanitor(SweepOptions options)
    {
        public ILogger<NetworkJanitor> Logger { private get; init; }

        public required NetworkSegment Network { private get; init; }

        private HashSet<NetworkHostContext> _sweepableHosts = [];

        private CancellationTokenSource? _sweepCancellation;

        public async void StartSweeping()
        {
            if (_sweepCancellation != null)
                throw new Exception("Sweeping already started.");

            var stoppingToken = (_sweepCancellation = new()).Token;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(options.Frequency, stoppingToken);

                    using (await Network.Mutex.LockAsync(stoppingToken))
                    {
                        SweepHostAddresses(Network);
                        SweepHosts(Network);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        internal void MakeHostEligibleForSweeping(NetworkHostContext host)
        {
            _sweepableHosts.Add(host);
        }

        internal void SweepHostAddresses(NetworkSegment network)
        {
            foreach (var host in network)
            {
                foreach (var ip in host.IPAddresses.ToArray())
                {
                    if (host.ShouldAddressExpire(ip, out var expires))
                    {
                        if (DateTime.Now - expires > options.Delay)
                        {
                            if (host.RemoveAddress(ip, true))
                            {
                                using var scope = Logger.BeginHostScope(host);

                                Logger.LogHostAddressRemoved(host, ip);
                            }
                        }
                    }
                }
            }
        }

        internal void SweepHosts(NetworkSegment network)
        {
            foreach (var ctx in _sweepableHosts.ToArray())
            {
                if (ctx.Host.FilterRefCount > 0)
                    continue;

                if (ctx.Host is NetworkRouter router)
                {
                    if (DateTime.Now < router.ValidUntil) // null will not match
                        continue;
                }

                ctx.Dispose();

                _sweepableHosts.Remove(ctx);
            }
        }

        public void StopSweeping()
        {
            if (_sweepCancellation == null)
                throw new Exception("Sweeping not yet started.");

            _sweepCancellation.Cancel();
            _sweepCancellation = null;
        }
    }
}
