using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.LaunchDaemon.Native
{
    /// <summary>
    /// IOKit power-management bindings (IOPMLib.h), validated on real hardware by
    /// probes/PowerProbe.MacOS. Like every binding in this namespace, nothing here needs a
    /// WindowServer connection — everything is callable from the launch daemon.
    /// System power messages live in <see cref="IOMessage"/>.
    /// </summary>
    public static partial class IOPM
    {
        /// <summary>Keeps the display (and thereby the system) from idle-sleeping (caffeinate -d).</summary>
        public const string kIOPMAssertionTypePreventUserIdleDisplaySleep   = "PreventUserIdleDisplaySleep";
        public const string kIOPMAssertionTypePreventUserIdleSystemSleep    = "PreventUserIdleSystemSleep";
        public const string kIOPMAssertionTypePreventSystemSleep            = "PreventSystemSleep";

        // per-assertion dictionary keys of IOPMCopyAssertionsByProcess (probe-confirmed)
        public const string kIOPMAssertionTypeKey = "AssertType";
        public const string kIOPMAssertionNameKey = "AssertName";
        public const string kIOPMAssertionDetailsKey = "Details";
        public const string kIOPMAssertionHumanReadableReasonKey = "HumanReadableReason";
        public const string kIOPMAssertionProcessNameKey = "Process Name";

        #region P/Invoke
        [LibraryImport(IOKit.Framework)]
        private static partial int IOPMAssertionCreateWithDescription(nint assertionType, nint name, nint details,
            nint humanReadableReason, nint localizationBundlePath, double timeout, nint timeoutAction, out uint assertionId);

        [LibraryImport(IOKit.Framework)]
        public static partial int IOPMAssertionRelease(uint assertionId);

        /// <summary>pid (CFNumber) -> CFArray of assertion CFDictionaries; NULL when no assertions exist.</summary>
        [LibraryImport(IOKit.Framework)]
        public static partial int IOPMCopyAssertionsByProcess(out nint assertionsByPid);

        [LibraryImport(IOKit.Framework)]
        public static partial int IOPMSleepEnabled(); // boolean_t

        [LibraryImport(IOKit.Framework)]
        public static partial uint IOPMFindPowerManagement(uint masterPort); // io_connect_t

        /// <summary>Forced software sleep (what `pmset sleepnow` uses); overrides all assertions. Root only.</summary>
        [LibraryImport(IOKit.Framework)]
        public static partial int IOPMSleepSystem(uint fb);

        // callback is an UnmanagedCallersOnly function pointer (AOT-safe, no delegate marshalling):
        //   void (*)(void* refCon, io_service_t service, uint32_t messageType, void* messageArgument)
        /// <summary>Creates its own notification port; returns the io_connect_t "root port" used for the acks (0 on failure).</summary>
        [LibraryImport(IOKit.Framework)]
        public static partial uint IORegisterForSystemPower(nint refCon, out nint notifyPort, nint callback, out uint notifier);

        [LibraryImport(IOKit.Framework)]
        public static partial int IODeregisterForSystemPower(ref uint notifier);

        [LibraryImport(IOKit.Framework)]
        public static partial int IOAllowPowerChange(uint rootPort, nint notificationId);
        #endregion

        #region power sources (IOPowerSources.h)
        /// <summary>Snapshot of all power sources; the caller owns it (+1, release via CFRelease).</summary>
        [LibraryImport(IOKit.Framework)]
        public static partial nint IOPSCopyPowerSourcesInfo();

        /// <summary>"AC Power", "Battery Power" or "UPS Power" as a borrowed constant CFString.</summary>
        [LibraryImport(IOKit.Framework)]
        public static partial nint IOPSGetProvidingPowerSourceType(nint snapshot);

        // callback is an UnmanagedCallersOnly function pointer (AOT-safe): void (*)(void* context)
        /// <summary>CFRunLoopSourceRef firing on power source changes; the caller owns it (+1).</summary>
        [LibraryImport(IOKit.Framework)]
        public static partial nint IOPSNotificationCreateRunLoopSource(nint callback, nint context);
        #endregion

        #region helpers
        /// <summary>Creates a power assertion; the caller owns the returned id (release via <see cref="IOPMAssertionRelease"/>).</summary>
        public static uint CreateAssertion(string type, string name, string? details)
        {
            nint cfType = CF.CreateString(type);
            nint cfName = CF.CreateString(name);
            nint cfDetails = details != null ? CF.CreateString(details) : 0;

            try
            {
                // HumanReadableReason is omitted — it would require a localization bundle path
                int rc = IOPMAssertionCreateWithDescription(cfType, cfName, cfDetails, 0, 0, 0, 0, out uint id);

                if (rc != 0)
                    throw new Exception($"IOPMAssertionCreateWithDescription({type}) failed (0x{rc:X8})");

                return id;
            }
            finally
            {
                CF.CFRelease(cfType);
                CF.CFRelease(cfName);

                if (cfDetails != 0)
                    CF.CFRelease(cfDetails);
            }
        }
        #endregion
    }
}
