namespace MadWizard.Desomnia.Daemon.DBus
{
    // Declares the well-known bus name and object path of the D-Bus object an interface represents.
    // Consumed by ContainerBuilder.RegisterDBusService<TInterface, TProxy>().
    [AttributeUsage(AttributeTargets.Interface)]
    internal sealed class DBusServiceAttribute(string serviceName, string objectPath) : Attribute
    {
        internal string ServiceName { get; } = serviceName;
        internal string ObjectPath { get; } = objectPath;
    }
}
