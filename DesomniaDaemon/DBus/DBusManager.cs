using MadWizard.Desomnia.Daemon.DBus.Manager;
using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace MadWizard.Desomnia.Daemon.DBus
{
    internal class DBusManager : IDisposable
    {
        public required ILogger<DBusManager> Logger { private get; init; }

        private Connection SystemBusConnection
        {
            get
            {
                if (field == null)
                {
                    field = new Connection(Address.System);
                    field.ConnectAsync().GetAwaiter().GetResult();

                    field.StateChanged += SystemBusConnection_StateChanged;
                }

                return field;
            }
        }

        private ILogin1Manager LoginManager
        {
            get
            {
                const string serviceName = "org.freedesktop.login1";
                const string objectPath = "/org/freedesktop/login1";

                if (field == null)
                {
                    field = SystemBusConnection.CreateProxy<ILogin1Manager>(serviceName, objectPath);
                }

                return field;
            }
        }

        private void SystemBusConnection_StateChanged(object? sender, ConnectionStateChangedEventArgs args)
        {
            switch (args.State)
            {
                case ConnectionState.Created:
                    break;
                case ConnectionState.Connecting:
                    break;
                case ConnectionState.Connected:
                    break;
                case ConnectionState.Disconnecting:
                    break;
                case ConnectionState.Disconnected:
                    break;
            }

            Logger.LogTrace("Connection to D-Bus established.");


            Logger.LogDebug("SystemBusConnection state changed to: {state}", e.State);
        }

        public void Dispose()
        {
            SystemBusConnection.Dispose();

            Logger.LogTrace("Disconnected from D-Bus.");
        }
    }
}
