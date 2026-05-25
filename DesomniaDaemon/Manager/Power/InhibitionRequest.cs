using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Power.Manager
{
    internal sealed class InhibitionRequest(string name, string? reason, InhibitionOperation operation, InhibitionMode mode) : IPowerRequest
    {
        public string               Name        => name;
        public string?              Reason      => reason;
        public InhibitionOperation  Operation   => operation;
        public InhibitionMode       Mode        => mode;

        public uint                 UID { get; init; }
        public uint                 PID { get; init; }

        public SafeHandle?          Handle { private get; init; }

        public void Dispose()
        {
            Handle?.Dispose();
        }
    }

    [Flags]
    public enum InhibitionOperation
    {
        Unknown             = 0,

        Sleep               = 1 << 1,   // Block suspend/hibernate
        Shutdown            = 1 << 2,   // Block shutdown/reboot/poweroff
        Reboot              = 1 << 3,   // Block reboot specifically
        Idle                = 1 << 4,   // Block idle-triggered actions

        HandlePowerKey      = 1 << 10,  // Block power button handling
        HandleSuspendKey    = 1 << 11,  // Block suspend button handling
        HandleHibernateKey  = 1 << 12,  // Block hibernate button handling
        HandleLidSwitch     = 1 << 13,  // Block laptop lid switch handling
    }

    [Flags]
    public enum InhibitionMode
    {
        Unknown             = 0,

        Delay               = 1 << 1,   // temporarily delay the operation
        BlockWeak           = 1 << 2,   // weaker variant of block
        Block               = 1 << 3,   // completely block the operation
    }

    internal static class Inhibition
    {
        internal static InhibitionOperation OfOperation(string operation) => operation switch
        {
            "sleep"                 => InhibitionOperation.Sleep,
            "shutdown"              => InhibitionOperation.Shutdown,
            "reboot"                => InhibitionOperation.Reboot,
            "idle"                  => InhibitionOperation.Idle,

            "handle-power-key"      => InhibitionOperation.HandlePowerKey,
            "handle-suspend-key"    => InhibitionOperation.HandleSuspendKey,
            "handle-hibernate-key"  => InhibitionOperation.HandleHibernateKey,
            "handle-lid-switch"     => InhibitionOperation.HandleLidSwitch,

            _                       => InhibitionOperation.Unknown
        };

        internal static InhibitionMode OfMode(string mode) => mode switch
        {
            "delay"                 => InhibitionMode.Delay,
            "block"                 => InhibitionMode.Block,
            "block-weak"            => InhibitionMode.BlockWeak,

            _                       => InhibitionMode.Unknown
        };
    }
}