using MadWizard.Desomnia.Network.Services;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Manager
{
    /**
     * On some platforms Wake-on-LAN cannot be enabled persistently.
     * Therefore the daemon will try to enable it each time, before going to sleep,
     * if not enabled otherwise.
     */
    internal class WakeOnLANEnabler(WakeOnLANMode set, IWakeOnLANManager? manager = null) : INetworkService
    {
        public required ILogger<WakeOnLANEnabler> Logger { private get; init; }

        private WakeOnLANMode? _modesToReset;

        void INetworkService.Startup()
        {
            if (manager is not null)
            {
                Logger.LogDebug("Automatically enabling Wake-on-LAN before suspend");
            }
            else
            {
                Logger.LogWarning("Automatically enabling Wake-on-LAN is not possible ('ethtool' is not installed)");
            }
        }

        void INetworkService.Suspend()
        {
            try
            {
                if (manager?.Modes is WakeOnLANMode modes && !modes.HasFlag(set))
                {
                    if (!manager.SupportedModes.HasFlag(set))
                    {
                        Logger.LogWarning("Wake-on-LAN (by {mode}) is not supported.", set);
                    }
                    else
                    {
                        manager.Modes = modes | set;

                        _modesToReset = modes;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Wake-on-LAN could not be enabled.");
            }
        }

        void INetworkService.Resume()
        {
            if (_modesToReset is WakeOnLANMode reset)
            {
                try
                {
                    manager?.Modes = reset;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Wake-on-LAN could not be resetted.");
                }
                finally
                {
                    _modesToReset = null;
                }
            }
        }

        void INetworkService.Shutdown() { } // don't call Suspend() here
    }
}