namespace MadWizard.Desomnia.LaunchDaemon.Native
{
    /// <summary>
    /// IOKit messages delivered to interest and system-power callbacks, collected in one place
    /// because the families span IOKit, IOPM and DCP (IOMessage.h: iokit_common_msg(x) = 0xE0000000 | x).
    /// </summary>
    public static class IOMessage
    {
        // system power messages (IOMessage.h)
        public const uint kIOMessageCanSystemSleep = 0xE0000270;
        public const uint kIOMessageSystemWillSleep = 0xE0000280;
        public const uint kIOMessageSystemWillNotSleep = 0xE0000290;
        public const uint kIOMessageSystemHasPoweredOn = 0xE0000300;
        public const uint kIOMessageSystemWillPowerOn = 0xE0000320;

        /// <summary>IOPMrootDomain general interest; messageArgument bit 0 = closed, bit 1 = causes sleep.</summary>
        public const uint kIOPMMessageClamshellStateChange = 0xE0034100;

        /// <summary>
        /// DCP AV link state message (undocumented, probe-observed on macOS 15):
        /// argument 0 = link down/display asleep, 1 = link training, 2 = actively driven.
        /// </summary>
        public const uint kIOMessageDCPAVLinkState = 0xE0115006;
    }
}
