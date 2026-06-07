namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    internal class SleepProxyLease : IDisposable
    {
        readonly IList<Context.Context> _tracked = [];

        public required TimeSpan Duration { get; init; }

        public void TrackContext(Context.Context context) => _tracked.Add(context);

        public void Dispose()
        {
            foreach (var context in _tracked.Reverse())
            {
                context.Dispose();
            }
        }
    }
}
