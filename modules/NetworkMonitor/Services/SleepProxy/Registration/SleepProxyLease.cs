using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;
using System.Net;

namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    internal class SleepProxyLease : IDisposable
    {
        public required ILogger<SleepProxyLease> Logger { private get; init; }

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

                _timer.Elapsed += (sender, args) => TriggerLeaseEnded();
            }
        }

        public TimeSpan Duration => GrantedUntil - DateTime.Now;

        public event EventHandler? Ended;

        public SleepProxyLease(TimeSpan duration)
        {
            GrantedUntil = DateTime.Now + duration;
        }

        public void AddInstanceForDisposal(IDisposable instance)
        {
            _disposables.Push(instance);
        }

        public async void Validate(RemoteHostWatch watch)
        {
            try
            {
                if (await watch.ValidateHandoff())
                {
                    this.Ended += (sender, args) => watch.Started -= OnRemoteHostStarted;

                    watch.Started += OnRemoteHostStarted; return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not validate lease.");
            }

            TriggerLeaseEnded();
        }

        private async Task OnRemoteHostStarted(Event data) => TriggerLeaseEnded();

        private void TriggerLeaseEnded()
        {
            if (_timer != null)
            {
                _timer?.Stop();
                _timer = null;

                Ended?.Invoke(this, EventArgs.Empty);
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

    class SleepProxyAddressLease(NetworkHost host, IPAddress ip) : IDisposable
    {
        void IDisposable.Dispose()
        {
            host.RemoveAddress(ip);
        }
    }
}
