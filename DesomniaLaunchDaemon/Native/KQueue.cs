using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.LaunchDaemon.Native
{
    /// <summary>
    /// Minimal kqueue bindings, for the one thing macOS *will* tell us about a process without an
    /// entitlement: that it has ended. EVFILT_PROC with NOTE_EXIT attaches to a pid we already know
    /// and needs neither root nor any special right — the kernel only checks credentials when
    /// NOTE_EXIT and NOTE_EXITSTATUS are asked for together, and the exit status is of no interest
    /// here. It says nothing about processes *starting*, which is why polling does not go away.
    ///
    /// Public so macOS-native plugins referencing the daemon can reuse the bindings.
    /// </summary>
    public static unsafe partial class KQueue
    {
        const string LibSystem = "/usr/lib/libSystem.B.dylib";

        /// <summary>&lt;sys/errno.h&gt;: the wait was interrupted by a signal and should simply be repeated.</summary>
        public const int EINTR = 4;

        #region <sys/event.h>
        public const short EVFILT_PROC = -5;
        public const short EVFILT_USER = -10;

        public const ushort EV_ADD = 0x0001;
        public const ushort EV_ENABLE = 0x0004;
        public const ushort EV_ONESHOT = 0x0010;
        public const ushort EV_CLEAR = 0x0020;
        public const ushort EV_RECEIPT = 0x0040;
        public const ushort EV_ERROR = 0x4000;

        public const uint NOTE_EXIT = 0x80000000;
        public const uint NOTE_TRIGGER = 0x01000000;

        /// <summary>struct kevent — 32 bytes, identical on arm64 and x86_64 (both LP64).</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct kevent
        {
            public nuint ident;     // uintptr_t – the pid, for EVFILT_PROC
            public short filter;    // int16_t
            public ushort flags;    // uint16_t
            public uint fflags;     // uint32_t
            public nint data;       // intptr_t – errno, on an EV_ERROR result
            public nint udata;      // void*
        }
        #endregion

        #region P/Invoke
        [LibraryImport(LibSystem, SetLastError = true)]
        private static partial int kqueue();

        // a null timeout blocks indefinitely; a null list with a count of 0 means "no changes"
        // named for C# rather than C: the struct below already claims the name "kevent" here
        [LibraryImport(LibSystem, SetLastError = true, EntryPoint = "kevent")]
        private static partial int Poll(int kq, kevent* changelist, int nchanges, kevent* eventlist, int nevents, nint timeout);

        [LibraryImport(LibSystem, SetLastError = true)]
        private static partial int close(int fd);
        #endregion

        #region helpers
        /// <summary>Opens a kernel event queue, or throws with the errno that stopped it.</summary>
        public static int Open()
        {
            int kq = kqueue();

            if (kq < 0)
                throw new InvalidOperationException($"kqueue failed: errno {Marshal.GetLastPInvokeError()}");

            return kq;
        }

        public static void Close(int kq) => close(kq);

        /// <summary>
        /// Applies one change and reports what the kernel made of it. EV_RECEIPT is what makes that
        /// possible: it forces the registration to report its own outcome into the event list
        /// instead of leaving a failure to be discovered as a missing notification later.
        /// </summary>
        private static bool TryApply(int kq, kevent change, out int error)
        {
            change.flags |= EV_RECEIPT;

            kevent receipt = default;

            if (Poll(kq, &change, 1, &receipt, 1, 0) < 0)
            {
                error = Marshal.GetLastPInvokeError();

                return false;
            }

            error = (receipt.flags & EV_ERROR) != 0 ? (int)receipt.data : 0;

            return error == 0;
        }

        /// <summary>Asks to be told when a process ends. ESRCH means it already has.</summary>
        public static bool TryWatchExit(int kq, int pid, out int error) => TryApply(kq, new kevent
        {
            ident = (nuint)pid,
            filter = EVFILT_PROC,
            flags = EV_ADD | EV_ONESHOT,
            fflags = NOTE_EXIT,
        }, out error);

        /// <summary>Registers the user event that a blocking <see cref="Wait"/> can be broken with.</summary>
        public static bool TryAddWakeup(int kq, int ident, out int error) => TryApply(kq, new kevent
        {
            ident = (nuint)ident,
            filter = EVFILT_USER,
            flags = EV_ADD | EV_CLEAR,
        }, out error);

        /// <summary>Triggers that user event, from any thread.</summary>
        public static bool TryWake(int kq, int ident, out int error) => TryApply(kq, new kevent
        {
            ident = (nuint)ident,
            filter = EVFILT_USER,
            flags = EV_ENABLE,
            fflags = NOTE_TRIGGER,
        }, out error);

        /// <summary>Blocks until something happens; returns the number of events, or -1 with errno set.</summary>
        public static int Wait(int kq, Span<kevent> events)
        {
            fixed (kevent* buffer = events)
            {
                return Poll(kq, null, 0, buffer, events.Length, 0);
            }
        }
        #endregion
    }
}
