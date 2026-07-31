using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Processes.Manager.Native
{
    /// <summary>
    /// The event-loop syscalls: epoll, eventfd and pidfd. Together they are Linux's answer to the
    /// one thing polling reports late — that a process has ended — and a pidfd needs no privilege
    /// whatsoever to open: the kernel checks flags, that the pid is positive, and that it exists.
    /// Permission is enforced when a pidfd is *used* to signal or steal descriptors, never to watch.
    ///
    /// Public so Linux-native plugins referencing the daemon can reuse the bindings.
    /// </summary>
    public static partial class Epoll
    {
        // "libc" alone is a gamble: .NET probes libc.so first, which on Debian and Raspberry Pi OS
        // is either an ld script from libc6-dev or missing altogether.
        const string LibC = "libc.so.6";

        /// <summary>&lt;asm-generic/errno.h&gt;: interrupted by a signal – and the runtime does signal its threads.</summary>
        public const int EINTR = 4;
        /// <summary>The process is gone. Expected: a pid can die between being listed and being watched.</summary>
        public const int ESRCH = 3;
        /// <summary>Out of descriptors – per-process (EMFILE) and system-wide (ENFILE).</summary>
        public const int EMFILE = 24, ENFILE = 23;

        public const int EPOLL_CLOEXEC = 0x80000; // == O_CLOEXEC
        public const int EPOLL_CTL_ADD = 1, EPOLL_CTL_DEL = 2;

        public const uint EPOLLIN = 0x001, EPOLLERR = 0x008, EPOLLHUP = 0x010;

        public const int EFD_CLOEXEC = 0x80000, EFD_NONBLOCK = 0x800;

        /// <summary>&lt;asm/unistd.h&gt;: pidfd_open, 434 on x86_64 and on the asm-generic table (arm64, armv7).</summary>
        public const long SYS_pidfd_open = 434;

        #region P/Invoke
        // glibc only grew a pidfd_open() wrapper in 2.36, one release above the floor this project
        // documents – so it goes through syscall(2), whose number is the same on every architecture
        // the daemon is published for
        [LibraryImport(LibC, EntryPoint = "syscall", SetLastError = true)]
        private static partial long Syscall(long number, long pid, long flags);

        [LibraryImport(LibC, SetLastError = true)]
        private static partial int epoll_create1(int flags);

        [LibraryImport(LibC, SetLastError = true)]
        private static partial int epoll_ctl(int epfd, int op, int fd, [In] byte[]? @event);

        [LibraryImport(LibC, SetLastError = true)]
        private static partial int epoll_wait(int epfd, [Out] byte[] events, int maxevents, int timeout);

        [LibraryImport(LibC, SetLastError = true)]
        private static partial int eventfd(uint initval, int flags);

        [LibraryImport(LibC, SetLastError = true)]
        private static partial nint read(int fd, [Out] byte[] buffer, nuint count);

        [LibraryImport(LibC, SetLastError = true)]
        private static partial nint write(int fd, [In] byte[] buffer, nuint count);

        [LibraryImport(LibC, SetLastError = true, EntryPoint = "close")]
        public static partial int Close(int fd);
        #endregion

        #region struct epoll_event
        /**
         * struct epoll_event is the one place where this ABI is not uniform.
         *
         * The kernel packs it on x86_64 and nowhere else – deliberately, so that the 64-bit struct
         * matches the 32-bit one and no compat translation is needed. Everywhere else the 64-bit
         * data field takes its natural 8-byte alignment, padding included. So the same struct is
         * 12 bytes with data at 4 on x64, and 16 bytes with data at 8 on arm64 and armv7 alike.
         *
         * A C# struct cannot express both, and the naive declaration silently produces the ARM
         * layout on x86_64 – where epoll still returns events but every cookie read back is
         * garbage. So the buffer is plain bytes and the offsets are worked out once.
         */
        public static (int Size, int DataOffset) EventLayout(Architecture architecture)
        {
            return architecture is Architecture.X64 or Architecture.X86 ? (12, 4) : (16, 8);
        }

        private static readonly (int Size, int DataOffset) Layout = EventLayout(RuntimeInformation.ProcessArchitecture);

        public static int EventSize => Layout.Size;

        /// <summary>Reads the 64-bit cookie of the n-th event out of a buffer filled by <see cref="Wait"/>.</summary>
        public static ulong EventData(byte[] events, int index)
        {
            return Unsafe.ReadUnaligned<ulong>(ref events[index * Layout.Size + Layout.DataOffset]);
        }

        private static byte[] Event(uint mask, ulong data)
        {
            var buffer = new byte[Layout.Size];

            Unsafe.WriteUnaligned(ref buffer[0], mask);
            Unsafe.WriteUnaligned(ref buffer[Layout.DataOffset], data);

            return buffer;
        }
        #endregion

        #region helpers
        /// <summary>Opens an epoll instance, or throws with the errno that stopped it.</summary>
        public static int Open()
        {
            int epoll = epoll_create1(EPOLL_CLOEXEC);

            if (epoll < 0)
                throw new InvalidOperationException($"epoll_create1 failed: errno {Marshal.GetLastPInvokeError()}");

            return epoll;
        }

        /// <summary>Opens the counter used to break a blocking <see cref="Wait"/> from another thread.</summary>
        public static int OpenEvent()
        {
            int fd = eventfd(0, EFD_CLOEXEC | EFD_NONBLOCK);

            if (fd < 0)
                throw new InvalidOperationException($"eventfd failed: errno {Marshal.GetLastPInvokeError()}");

            return fd;
        }

        /// <summary>A descriptor for a process, which stays readable from the moment it exits.</summary>
        public static int OpenProcess(int pid) => (int)Syscall(SYS_pidfd_open, pid, 0);

        /// <summary>Watches a descriptor for readability, tagging it with a cookie to recognise it by.</summary>
        public static bool TryWatch(int epoll, int fd, ulong data)
        {
            // EPOLLERR and EPOLLHUP are reported whether or not they are asked for
            return epoll_ctl(epoll, EPOLL_CTL_ADD, fd, Event(EPOLLIN, data)) == 0;
        }

        public static void Unwatch(int epoll, int fd) => epoll_ctl(epoll, EPOLL_CTL_DEL, fd, null);

        /// <summary>Blocks until something is ready; returns the number of events, or -1 with errno set.</summary>
        public static int Wait(int epoll, byte[] events, int timeout = -1)
        {
            return epoll_wait(epoll, events, events.Length / Layout.Size, timeout);
        }

        /// <summary>Raises the counter, waking whatever is blocked in <see cref="Wait"/>.</summary>
        public static void Signal(int fd)
        {
            var one = new byte[8];

            Unsafe.WriteUnaligned(ref one[0], 1UL); // an eventfd reads and writes exactly eight bytes

            write(fd, one, 8);
        }

        /// <summary>Empties the counter so it stops reporting itself ready.</summary>
        public static void Drain(int fd) => read(fd, new byte[8], 8);
        #endregion
    }
}
