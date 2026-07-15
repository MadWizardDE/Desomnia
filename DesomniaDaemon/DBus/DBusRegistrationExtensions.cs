using Autofac;
using Autofac.Builder;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MadWizard.Desomnia.Daemon.DBus
{
    internal static class DBusRegistrationExtensions
    {
        /// <summary>
        /// Registers <typeparamref name="TProxy"/> as <typeparamref name="TInterface"/>, supplying the
        /// bus coordinates declared by the interface's <see cref="DBusServiceAttribute"/> to the
        /// proxy constructor (parameters must be named <c>serviceName</c> and <c>objectPath</c>).
        /// </summary>
        public static IRegistrationBuilder<TProxy, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            RegisterDBusService<TInterface, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProxy>(this ContainerBuilder builder)
                where TInterface : notnull
                where TProxy : TInterface
        {
            var service = typeof(TInterface).GetCustomAttribute<DBusServiceAttribute>()
                ?? throw new InvalidOperationException($"{typeof(TInterface).Name} is not annotated with [DBusService].");

            return builder.RegisterType<TProxy>()
                .WithParameter(new NamedParameter("serviceName", service.ServiceName))
                .WithParameter(new NamedParameter("objectPath", service.ObjectPath))
                .As<TInterface>()
                .SingleInstance();
        }
    }
}
