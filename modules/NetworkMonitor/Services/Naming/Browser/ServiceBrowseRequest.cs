using Makaretu.Dns;
using System.Threading.Channels;

namespace MadWizard.Desomnia.Network.Naming
{
    /// <summary>
    /// Describes a DNS-SD browse: the service type to look for, the record types to resolve (each flagged
    /// required or optional), and a <see cref="CancellationToken"/> bounding how long the browse runs --
    /// typically the application lifetime, for continual discovery. A <em>required</em> record type is
    /// actively queried for when it is not already present; an <em>optional</em> one is only picked up if
    /// it happens to be advertised. Disposing the request, or cancelling its token, ends the stream.
    /// </summary>
    public sealed class ServiceBrowseRequest : IAsyncEnumerable<ServiceInstance>, IDisposable
    {
        private readonly Channel<ServiceInstance> _channel = Channel.CreateUnbounded<ServiceInstance>(new() { SingleReader = true });

        private readonly CancellationTokenSource _cancellation;

        public ServiceBrowseRequest(DomainName serviceDomainName, CancellationToken cancellation = default)
        {
            ServiceDomainName = serviceDomainName;

            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            _cancellation.Token.Register(() =>
            {
                if (_channel.Writer.TryComplete())
                {
                    Completed?.Invoke(this, EventArgs.Empty);

                    _cancellation.Dispose();
                }
            }); 
        }

        public DomainName ServiceDomainName { get; } // _http._tcp.local

        public bool HasRequested(DomainName domainName) => ServiceDomainName == domainName;

        #region Continuous querying (RFC 6762 §5.2)
        private static readonly TimeSpan InitialRequeryInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxRequeryInterval     = TimeSpan.FromMinutes(60);

        private TimeSpan _requeryInterval = InitialRequeryInterval;
        private DateTime _requeryAt       = DateTime.Now + InitialRequeryInterval;

        /// <summary>
        /// Reports whether the browse is due to be re-issued, advancing the schedule by doubling the
        /// interval (capped at one hour) each time -- the back-off mandated by RFC 6762 §5.2.
        /// </summary>
        internal bool ShouldRequery(DateTime now)
        {
            if (now < _requeryAt)
                return false;

            _requeryAt = now + _requeryInterval;
            _requeryInterval = TimeSpan.FromTicks(Math.Min(_requeryInterval.Ticks * 2, MaxRequeryInterval.Ticks));

            return true;
        }
        #endregion

        internal event EventHandler? Completed;

        internal void Enqueue(ServiceInstance instance) => _channel.Writer.TryWrite(instance);

        IAsyncEnumerator<ServiceInstance> IAsyncEnumerable<ServiceInstance>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return _channel.Reader.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        }

        public void Dispose()
        {
            if (!_cancellation.IsCancellationRequested)
            {
                _cancellation.Cancel();
            }
        }
    }
}
