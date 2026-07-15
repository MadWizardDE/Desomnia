using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;

namespace MadWizard.Desomnia.Daemon.DBus
{
    internal class DBusManager : IDisposable
    {
        public required ILogger<DBusManager> Logger { private get; init; }

        internal DBusConnection SystemBusConnection
        {
            get
            {
                if (field == null)
                {
                    var address = DBusAddress.System
                        ?? throw new InvalidOperationException("No D-Bus system bus address available.");

                    Logger.LogTrace("Connecting to D-Bus...");

                    field = new DBusConnection(address);
                    field.ConnectAsync().AsTask().GetAwaiter().GetResult();

                    Logger.LogTrace("Connection to D-Bus established.");

                    _ = LogDisconnectAsync(field);
                }

                return field;
            }

            private set;
        }

        private async Task LogDisconnectAsync(DBusConnection connection)
        {
            var reason = await connection.DisconnectedAsync();

            if (reason is null) // connection was disposed
                Logger.LogTrace("Disconnected from D-Bus.");
            else
                Logger.LogError(reason, "Disconnected from D-Bus.");
        }

        public void Dispose()
        {
            SystemBusConnection.Dispose();
            SystemBusConnection = null!;
        }
    }
}
