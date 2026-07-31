using MadWizard.Desomnia.LaunchDaemon.Native;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Processes.Manager
{
    /// <summary>
    /// One kernel event queue and one thread blocked on it, reporting process exits the moment they
    /// happen instead of at the next poll. Costs nothing while nothing exits.
    ///
    /// This is the half of ETW that macOS will give away for free. It cannot report a process
    /// *starting* — NOTE_TRACK has answered ENOTSUP since 10.5 — so the enumeration keeps that job,
    /// but everything about a process ending arrives here first, including the one case polling
    /// gets wrong: a process that exits under a parent that never reaps it stays in the kernel's
    /// pid list as a zombie, and only this says otherwise.
    /// </summary>
    internal sealed class KQueueProcessExitWatcher : IDisposable
    {
        /// <summary>The user-event id whose only purpose is to break the blocking wait on shutdown.</summary>
        private const int Wakeup = 1;

        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<int, Action> _watched = [];

        private readonly int _kq;
        private readonly Thread _thread;

        private volatile bool _stopping;

        public KQueueProcessExitWatcher(ILogger logger)
        {
            _logger = logger;

            _kq = KQueue.Open();

            try
            {
                if (!KQueue.TryAddWakeup(_kq, Wakeup, out int error))
                    throw new InvalidOperationException($"registering the kqueue wake-up failed: errno {error}");
            }
            catch
            {
                KQueue.Close(_kq);

                throw;
            }

            _thread = new Thread(Run) { Name = nameof(KQueueProcessExitWatcher), IsBackground = true };
            _thread.Start();

            _logger.LogDebug("Watching for process exits through kqueue");
        }

        /// <summary>
        /// Asks to be told when <paramref name="pid"/> ends. A pid that ended between being
        /// discovered and being registered is not an error worth raising – the enumeration will
        /// notice on its own next time round.
        /// </summary>
        public void Watch(int pid, Action onExit)
        {
            _watched[pid] = onExit;

            if (!KQueue.TryWatchExit(_kq, pid, out int error))
            {
                _watched.TryRemove(pid, out _);

                _logger.LogTrace("Not watching {pid} for exit: errno {error}", pid, error);
            }
        }

        private void Run()
        {
            Span<KQueue.kevent> events = stackalloc KQueue.kevent[16];

            while (!_stopping)
            {
                int count = KQueue.Wait(_kq, events);

                if (count < 0)
                {
                    int error = Marshal.GetLastPInvokeError();

                    if (error == KQueue.EINTR)
                        continue;

                    _logger.LogWarning("kevent failed: errno {error}. Process exits fall back to the poll interval.", error);

                    return;
                }

                for (int i = 0; i < count && !_stopping; i++)
                {
                    Deliver(events[i]);
                }
            }
        }

        private void Deliver(KQueue.kevent @event)
        {
            if (@event.filter == KQueue.EVFILT_USER)
                return; // the shutdown wake-up; the loop condition takes it from here

            if ((@event.flags & KQueue.EV_ERROR) != 0)
            {
                _logger.LogTrace("kqueue reported errno {error} for {pid}", (int)@event.data, (int)@event.ident);

                return;
            }

            if (@event.filter != KQueue.EVFILT_PROC || (@event.fflags & KQueue.NOTE_EXIT) == 0)
                return;

            if (_watched.TryRemove((int)@event.ident, out Action? onExit))
            {
                try
                {
                    // deliberately on this thread: reporting exits in the order the kernel gave
                    // them matters more than reporting them concurrently, and the manager's
                    // bookkeeping is the same either way
                    onExit();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reporting the exit of {pid} failed", (int)@event.ident);
                }
            }
        }

        public void Dispose()
        {
            _stopping = true;

            KQueue.TryWake(_kq, Wakeup, out _);

            _thread.Join(TimeSpan.FromSeconds(5));

            KQueue.Close(_kq);

            _watched.Clear();
        }
    }
}
