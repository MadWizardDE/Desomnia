using MadWizard.Desomnia.Daemon.DBus.Interface;
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
                    field.StateChanged += SystemBusConnection_StateChanged;
                    field.ConnectAsync().GetAwaiter().GetResult();
                }

                return field;
            }
        }

        internal ILogin1Manager LoginManager // TODO: make this generic
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
                    Logger.LogTrace("D-Bus connection created.");
                    break;

                case ConnectionState.Connecting:
                    Logger.LogTrace("Connecting to D-Bus...");
                    break;
                case ConnectionState.Connected:
                    var info = args.ConnectionInfo;
                    Logger.LogTrace("Connection to D-Bus established." 
                        + (info.RemoteIsBus ? $" ('{info.LocalName}')" : ""));
                    break;

                case ConnectionState.Disconnecting:
                    Logger.LogTrace("Disconnecting from D-Bus...");
                    break;
                case ConnectionState.Disconnected:
                    if (args.DisconnectReason is not null)
                        Logger.LogError(args.DisconnectReason, "Disconnected from D-Bus.");
                    else
                        Logger.LogTrace("Disconnected from D-Bus.");

                    break;
            }
        }

        public void Dispose()
        {
            SystemBusConnection.StateChanged -= SystemBusConnection_StateChanged;
            SystemBusConnection.Dispose();

            Logger.LogTrace("Disconnected from D-Bus.");
        }
    }
}
