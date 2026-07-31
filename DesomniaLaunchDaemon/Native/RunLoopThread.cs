using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.LaunchDaemon.Native
{
    /// <summary>
    /// Base for managers that consume IOKit notifications: owns a dedicated background thread
    /// running a CFRunLoop, the startup handshake (errors from <see cref="Initialize"/> rethrow
    /// on the starting thread), and the GCHandle whose IntPtr round-trips through native refCon
    /// parameters back to the instance ([UnmanagedCallersOnly] callbacks are static function
    /// pointers — AOT-safe, no delegate marshalling).
    ///
    /// <see cref="Initialize"/> and all notification callbacks run on the loop thread.
    /// <see cref="Stop"/> stops the loop, joins the thread, then runs <see cref="Cleanup"/>
    /// to release native registrations — so cleanup never races a callback.
    /// </summary>
    public abstract class RunLoopThread : IDisposable
    {
        private readonly object _lock = new();

        private Thread? _thread;
        private ManualResetEventSlim? _ready;
        private Exception? _startupError;
        private GCHandle _self;

        /// <summary>The CFRunLoop of the notification thread; valid inside <see cref="Initialize"/> and callbacks.</summary>
        protected nint RunLoop { get; private set; }

        /// <summary>Passed as refCon to native registrations; resolve back via <see cref="Self{T}"/>.</summary>
        protected nint RefCon => GCHandle.ToIntPtr(_self);

        /// <summary>Resolves the instance behind a native refCon inside an [UnmanagedCallersOnly] callback.</summary>
        protected static T Self<T>(nint refCon) where T : RunLoopThread => (T)GCHandle.FromIntPtr(refCon).Target!;

        /// <summary>Spawns the loop thread on first call (thread-safe) and waits for <see cref="Initialize"/> to complete.</summary>
        protected void EnsureStarted(CancellationToken token = default)
        {
            lock (_lock)
            {
                if (_thread == null)
                {
                    _ready = new ManualResetEventSlim();
                    _self = GCHandle.Alloc(this);

                    _thread = new Thread(Run) { Name = GetType().Name, IsBackground = true };
                    _thread.Start();
                }
            }

            _ready!.Wait(token);

            if (_startupError != null)
                throw new Exception($"Startup of {GetType().Name} failed.", _startupError);
        }

        private void Run()
        {
            try
            {
                RunLoop = CF.CFRunLoopGetCurrent();

                Initialize();
            }
            catch (Exception ex)
            {
                _startupError = ex;

                _ready!.Set();

                return;
            }

            _ready!.Set();

            CF.CFRunLoopRun();
        }

        /// <summary>Registers notifications and adds their sources to <see cref="RunLoop"/> (runs on the loop thread).</summary>
        protected abstract void Initialize();

        /// <summary>Releases native registrations; runs after the loop has stopped and the thread joined.</summary>
        protected virtual void Cleanup() { }

        protected void Stop()
        {
            Thread? thread;

            lock (_lock)
            {
                thread = _thread;

                _thread = null;
            }

            if (thread == null)
                return;

            if (RunLoop != 0)
                CF.CFRunLoopStop(RunLoop);

            thread.Join(TimeSpan.FromSeconds(5));

            Cleanup();

            if (_self.IsAllocated)
                _self.Free();
        }

        public virtual void Dispose() => Stop();
    }
}
