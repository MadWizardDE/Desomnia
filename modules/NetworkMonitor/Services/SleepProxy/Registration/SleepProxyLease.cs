using Autofac.Core;
using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;

using Timer = System.Timers.Timer;

namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    internal class SleepProxyLease : IDisposable, IDisposer
    {
        public required ILogger<SleepProxyLease> Logger { private get; init; }

        private Timer? _timer;

        readonly IList<IDisposable> _disposables = [];

        public required byte Sequence { get; init; }

        public DateTime GrantedUntil
        {
            get; set
            {
                if (value < DateTime.Now)
                    throw new ArgumentException(nameof(GrantedUntil));

                field = value;

                _timer?.Dispose();

                _timer = new Timer(Duration)
                {
                    AutoReset = false,
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

        public void AddInstanceForDisposal(IDisposable disposable) => _disposables.Add(disposable);
        public void AddInstanceForAsyncDisposal(IAsyncDisposable disposable) => throw new NotImplementedException();

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

        public ValueTask DisposeAsync() => throw new NotImplementedException();

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;

            foreach (var context in _disposables.Reverse())
            {
                context.Dispose();
            }
        }
    }
}
