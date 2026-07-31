using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.LaunchDaemon.Native
{
    /// <summary>
    /// Asking a process to stop, and making it.
    ///
    /// SIGTERM is as close as this platform comes to the close message Windows sends a window: a
    /// process that wants to unwind cleanly can catch it, and one that does not simply dies. It is
    /// what launchd sends a job it is stopping, and what macOS sends its daemons at shutdown,
    /// before following up with the signal nothing can catch.
    ///
    /// It is *not* what makes a Mac application offer to save your documents — that is a quit
    /// Apple event, which a launch daemon has no way to send: the event would have to reach into a
    /// login session it cannot see, and would need an automation consent that nothing without a
    /// user interface can be granted. A per-session agent could do it; this cannot.
    /// </summary>
    public static partial class Signals
    {
        const string LibSystem = "/usr/lib/libSystem.B.dylib";

        /// <summary>&lt;sys/signal.h&gt;: ask politely, and end it. Neither number has ever varied.</summary>
        public const int SIGTERM = 15, SIGKILL = 9;

        /// <summary>&lt;sys/errno.h&gt;: there is no such process – which is to say, it stopped first.</summary>
        public const int ESRCH = 3;

        [LibraryImport(LibSystem, SetLastError = true)]
        private static partial int kill(int pid, int sig);

        /// <summary>
        /// Signals a process. As root that succeeds for anything but pid 1 and the binaries the
        /// system protects from being signalled at all, both of which report EPERM.
        /// </summary>
        public static bool TrySend(int pid, int signal, out int error)
        {
            if (kill(pid, signal) == 0)
            {
                error = 0;

                return true;
            }

            error = Marshal.GetLastPInvokeError();

            return false;
        }
    }
}
