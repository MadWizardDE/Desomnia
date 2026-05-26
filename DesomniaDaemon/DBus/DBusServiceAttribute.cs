namespace MadWizard.Desomnia.Daemon.DBus
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    internal sealed class DBusServiceAttribute(string objectPath) : Attribute
    {
        internal string ObjectPath { get; } = objectPath;
    }
}
