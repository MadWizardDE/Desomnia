using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Processes.Manager.Native
{
    /// <summary>
    /// Asking a process to stop, and making it.
    ///
    /// SIGTERM is as close as this platform comes to the close message Windows sends a window: a
    /// process that wants to unwind cleanly can catch it, and one that does not simply dies. It is
    /// what systemd sends a unit it is stopping, before following up with the signal nothing can
    /// catch — and a desktop application will only act on it if it installed a handler, since
    /// neither GTK nor Qt does that for you.
    /// </summary>
    public static partial class Signals
    {
        const string LibC = "libc.so.6";

        /// <summary>&lt;asm-generic/signal.h&gt;: ask politely, and end it. Neither number has ever varied.</summary>
        public const int SIGTERM = 15, SIGKILL = 9;

        /// <summary>&lt;asm-generic/errno.h&gt;: there is no such process – which is to say, it stopped first.</summary>
        public const int ESRCH = 3;

        [LibraryImport(LibC, SetLastError = true)]
        private static partial int kill(int pid, int sig);

        /// <summary>
        /// Signals a process. As root that succeeds for anything the kernel will let us signal;
        /// pid 1 and processes a security module protects report EPERM.
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
