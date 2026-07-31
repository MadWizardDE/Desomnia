using Autofac;
using Autofac.Builder;

namespace MadWizard.Desomnia
{
    public static class OrderedRegistrationExtensions
    {
        /// <summary>
        /// Registration metadata key holding the <see cref="int"/> position that
        /// <see cref="IOrderedCollection{T}"/> sorts by.
        /// </summary>
        public const string OrderKey = "Desomnia.Order";

        /// <summary>
        /// Attaches ordering metadata to an arbitrary registration, defining its position when the
        /// component is resolved as part of an <see cref="IOrderedCollection{T}"/>. Lower values come
        /// first; unordered registrations default to 0.
        /// </summary>
        public static IRegistrationBuilder<TLimit, TActivatorData, TStyle> WithOrder<TLimit, TActivatorData, TStyle>(
            this IRegistrationBuilder<TLimit, TActivatorData, TStyle> registration, int order)
        {
            return registration.WithMetadata(OrderKey, order);
        }
    }
}
