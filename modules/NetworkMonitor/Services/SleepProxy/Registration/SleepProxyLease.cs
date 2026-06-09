using Autofac.Core;

namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    internal class SleepProxyLease : IDisposable, IDisposer
    {
        readonly IList<IDisposable> _disposables = [];

        public required TimeSpan Duration { get; init; }

        public void AddInstanceForDisposal(IDisposable disposable) => _disposables.Add(disposable);
        public void AddInstanceForAsyncDisposal(IAsyncDisposable disposable) => throw new NotImplementedException();

        public ValueTask DisposeAsync() => throw new NotImplementedException();

        public void Dispose()
        {
            foreach (var context in _disposables.Reverse())
            {
                context.Dispose();
            }
        }
    }
}
