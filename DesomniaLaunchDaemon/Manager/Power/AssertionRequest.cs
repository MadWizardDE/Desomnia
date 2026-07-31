using MadWizard.Desomnia.LaunchDaemon.Native;

namespace MadWizard.Desomnia.Power.Manager
{
    /// <summary>
    /// An IOPM power assertion as an <see cref="IPowerRequest"/> — either created (and owned)
    /// by Desomnia itself, or a read-only snapshot of another process's assertion obtained
    /// through enumeration.
    /// </summary>
    internal sealed class AssertionRequest(string name, string? reason, string type) : IPowerRequest
    {
        public string   Name    => name;
        public string?  Reason  => reason;
        public string   Type    => type;

        public long     PID { get; init; }
        public string?  ProcessName { get; init; }

        /// <summary>Assertion id owned by this request; released on dispose (snapshots have none).</summary>
        public uint?    AssertionId { private get; init; }

        public void Dispose()
        {
            if (AssertionId is uint id)
                IOPM.IOPMAssertionRelease(id);
        }

        public override string ToString()
        {
            return $"AssertionRequest(who='{ProcessName ?? name}', why='{reason}', type={type})";
        }
    }

    /// <summary>
    /// The IOPM assertion type Desomnia uses to keep the system awake on demand (IOPMLib.h).
    /// The enum member names are the literal IOKit assertion type strings.
    /// </summary>
    public enum SleepAssertion
    {
        /// <summary>Prevents idle sleep while fully awake; NOT honored in dark wake (caffeinate -i).</summary>
        PreventUserIdleSystemSleep,

        /// <summary>Prevents system sleep, also holding the system up from a dark wake; honored on AC power only (caffeinate -s).</summary>
        PreventSystemSleep,

        /// <summary>Declares the system to be serving network clients; honored on AC power only (what the sharing services hold).</summary>
        NetworkClientActive,
    }
}
