using System.Runtime.InteropServices;
using System.Text;

namespace MadWizard.Desomnia.LaunchDaemon.Native
{
    /// <summary>
    /// Minimal libproc bindings. They are what the runtime's own process enumeration is built on,
    /// except that the runtime insists on describing every process it lists — including one call
    /// per thread of every process — before it will hand back so much as an id. Listing ids and
    /// describing a process are separate calls here, which is the whole point: a poll that finds
    /// nothing new costs exactly one syscall.
    ///
    /// Both flavors used below are on the kernel's NO_CHECK_SAME_USER list, so they answer for
    /// processes of other users without privileges. That matters: the same-user-gated
    /// PROC_PIDTBSDINFO would leave a daemon started outside launchd silently seeing only its own
    /// processes.
    ///
    /// Public so macOS-native plugins referencing the daemon can reuse the bindings.
    /// </summary>
    public static unsafe partial class LibProc
    {
        /// <summary>Not a file on disk since Big Sur — the dyld shared cache resolves it. Never preflight with File.Exists.</summary>
        const string LibProcDylib = "/usr/lib/libproc.dylib";
        const string LibSystem = "/usr/lib/libSystem.B.dylib";

        /// <summary>&lt;sys/param.h&gt;: the length the kernel truncates a command name to.</summary>
        public const int MAXCOMLEN = 16;

        /// <summary>&lt;sys/proc_info.h&gt;: the flavor of proc_pidinfo that any user may ask about any process.</summary>
        public const int PROC_PIDT_SHORTBSDINFO = 13;

        /// <summary>&lt;sys/proc_info.h&gt;: 4 * MAXPATHLEN. proc_pidpath insists on 1024..4096 — anything else fails.</summary>
        public const int PROC_PIDPATHINFO_MAXSIZE = 4 * 1024;

        #region P/Invoke
        // returns the number of pids written – libproc has already divided the kernel's byte count
        // by sizeof(int) – or, called with a null buffer, the number it currently expects to need.
        // 0 means failure, never -1.
        [LibraryImport(LibProcDylib, EntryPoint = "proc_listallpids", SetLastError = true)]
        private static partial int proc_listallpids_size(nint buffer, int buffersize);

        [LibraryImport(LibProcDylib, SetLastError = true)]
        private static partial int proc_listallpids([Out] int[] buffer, int buffersize);

        // returns the number of bytes written, 0 on failure
        [LibraryImport(LibProcDylib, SetLastError = true)]
        private static partial int proc_pidinfo(int pid, int flavor, ulong arg, out proc_bsdshortinfo buffer, int buffersize);

        // returns strlen of the path (no NUL), 0 on failure
        [LibraryImport(LibProcDylib, SetLastError = true)]
        private static partial int proc_pidpath(int pid, [Out] byte[] buffer, uint buffersize);

        // the POSIX session (the session leader's pid) of an arbitrary process; no permission check
        [LibraryImport(LibSystem)]
        private static partial int getsid(int pid);
        #endregion

        /// <summary>
        /// &lt;sys/proc_info.h&gt;: struct proc_bsdshortinfo. All scalars, 64 bytes, no padding —
        /// the layout is kernel ABI and identical on arm64 and x86_64.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct proc_bsdshortinfo
        {
            public uint pbsi_pid;
            public uint pbsi_ppid;
            public uint pbsi_pgid;      // the process group, NOT a session
            public uint pbsi_status;
            public fixed byte pbsi_comm[MAXCOMLEN];
            public uint pbsi_flags;
            public uint pbsi_uid;
            public uint pbsi_gid;
            public uint pbsi_ruid;
            public uint pbsi_rgid;
            public uint pbsi_svuid;
            public uint pbsi_svgid;
            public uint pbsi_rfu;

            /// <summary>
            /// The command name, which the kernel keeps at 15 characters plus its NUL. Callers
            /// should prefer the executable name from <see cref="GetProcessPath"/> and fall back
            /// to this only where the path cannot be read.
            /// </summary>
            public string GetCommand()
            {
                fixed (byte* comm = pbsi_comm)
                {
                    var span = new ReadOnlySpan<byte>(comm, MAXCOMLEN);

                    int end = span.IndexOf((byte)0);

                    return Encoding.UTF8.GetString(end >= 0 ? span[..end] : span);
                }
            }
        }

        /// <summary>
        /// The pids of every process on the machine — one syscall, one int array, nothing else.
        /// </summary>
        /// <remarks>
        /// The kernel sizes the buffer for the moment it is asked and truncates silently if it
        /// turns out too small, so we ask for headroom and grow again whenever the buffer came
        /// back exactly full, which is the only signal a truncation gives.
        ///
        /// The list includes zombies and pid 0; describing a pid is what sorts those out.
        /// </remarks>
        public static IEnumerable<int> EnumeratePIDs()
        {
            int count = proc_listallpids_size(0, 0);

            if (count <= 0)
                throw new InvalidOperationException($"proc_listallpids failed: errno {Marshal.GetLastPInvokeError()}");

            int[] pids;

            do
            {
                pids = new int[count + 32];

                count = proc_listallpids(pids, pids.Length * sizeof(int));

                if (count <= 0)
                    throw new InvalidOperationException($"proc_listallpids failed: errno {Marshal.GetLastPInvokeError()}");
            }
            while (count == pids.Length);

            return pids[..count];
        }

        /// <summary>
        /// The short BSD info of a single process, or null if it is gone – which a pid straight
        /// from <see cref="EnumeratePIDs"/> may well be: the list carries zombies, and processes
        /// exit between the two calls.
        /// </summary>
        public static proc_bsdshortinfo? GetProcessInfo(int pid)
        {
            // pid 0 is the kernel, which several flavors refuse to describe at all
            if (pid <= 0)
                return null;

            if (proc_pidinfo(pid, PROC_PIDT_SHORTBSDINFO, 0, out proc_bsdshortinfo info, sizeof(proc_bsdshortinfo)) != sizeof(proc_bsdshortinfo))
                return null;

            return info;
        }

        /// <summary>
        /// The path of a process' executable image, or null if it cannot be read. For an app
        /// bundle this is the binary inside it, not the bundle: …/Foo.app/Contents/MacOS/Foo.
        /// </summary>
        public static string? GetProcessPath(int pid)
        {
            var buffer = new byte[PROC_PIDPATHINFO_MAXSIZE];

            int length = proc_pidpath(pid, buffer, (uint)buffer.Length);

            // the call reports strlen, so exactly that many bytes are the path
            return length > 0 ? Encoding.UTF8.GetString(buffer, 0, length) : null;
        }

        /// <summary>The POSIX session id of a process, or -1. Not a Windows terminal session — macOS has no such notion here.</summary>
        public static int GetSessionId(int pid) => getsid(pid);
    }
}
