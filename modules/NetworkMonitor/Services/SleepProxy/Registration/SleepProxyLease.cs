using MadWizard.Desomnia.Network.Neighborhood;
using Microsoft.Extensions.Logging;
using System.Net;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    internal class SleepProxyLease : IDisposable
    {
        private ScheduledTimer? _timer;

        readonly Stack<IDisposable> _disposables = [];

        public required byte Sequence { get; init; }

        public DateTime GrantedUntil
        {
            get; set
            {
                if (value < DateTime.Now)
                    throw new ArgumentException(nameof(GrantedUntil));

                field = value;

                _timer?.Dispose();

                _timer = new ScheduledTimer(field)
                {
                    Enabled = true
                };

                _timer.Elapsed += (sender, args) => Stop(true);
            }
        }

        public TimeSpan Duration => GrantedUntil - DateTime.Now;

        public event EventHandler<SleepProxyLeaseEndEventArgs>? Ended;

        public SleepProxyLease(TimeSpan duration)
        {
            GrantedUntil = DateTime.Now + duration;
        }

        public void AddInstanceForDisposal(IDisposable instance)
        {
            _disposables.Push(instance);
        }

        internal void Stop(bool expired = false)
        {
            if (_timer != null)
            {
                _timer?.Stop();
                _timer = null;

                Ended?.Invoke(this, new(expired));
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;

            while (_disposables.Count > 0)
            {
                var item = _disposables.Pop();

                try
                {
                    item.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // we can safely ignore this here
                }
            }
        }
    }

    class SleepProxyAddressLease(ILogger logger, NetworkHost host, IPAddress ip) : IDisposable
    {
        void IDisposable.Dispose()
        {
            if (host.RemoveAddress(ip))
            {
                logger.LogHostAddressRemoved(host, ip);
            }
        }
    }

    class SleepProxyLeaseEndEventArgs(bool expired = false) : EventArgs
    {
        public bool HasExpired => expired;
    }
}
