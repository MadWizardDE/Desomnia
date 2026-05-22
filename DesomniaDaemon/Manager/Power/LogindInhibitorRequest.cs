using Runtime = System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Power.Manager
{
    internal sealed class LogindInhibitorRequest : IPowerRequest
    {
        private readonly Runtime.SafeHandle? _handle;

        // For requests created by this daemon via Inhibit() — holds the fd open.
        internal LogindInhibitorRequest(string name, string? reason, Runtime.SafeHandle handle)
        {
            Name = name;
            Reason = reason;
            _handle = handle;
        }

        // For external inhibitors read from ListInhibitors() — no fd to close.
        internal LogindInhibitorRequest(string name, string? reason)
        {
            Name = name;
            Reason = reason;
        }

        public string Name { get; }
        public string? Reason { get; }

        public void Dispose() => _handle?.Dispose();
    }
}
