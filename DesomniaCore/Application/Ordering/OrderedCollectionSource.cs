using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Features.Metadata;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace MadWizard.Desomnia
{
    /// <summary>
    /// Provides <see cref="IOrderedCollection{T}"/> for any service type: the same components the built-in
    /// <see cref="IEnumerable{T}"/> relationship would yield, but sorted by the order metadata attached via
    /// <see cref="OrderedRegistrationExtensions.WithOrder"/> (missing metadata counts as 0; equal orders
    /// keep their registration order — the sort is stable).
    ///
    /// The collection is assembled from the loosely-typed <see cref="Meta{T}"/> relationship (value +
    /// string-keyed dictionary), so no metadata view type is involved — AOT-safe without the
    /// <see cref="AOTMetadataViewSource"/> detour, because the order value only ever lives in an
    /// <see cref="object"/> box. Resolving through the context at activation time also picks up
    /// registrations added in child scopes, and <see cref="IServiceWithType.ChangeType"/> preserves
    /// service keys. Registered once in <c>ApplicationBuilder.ConfigureContainer</c>.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "OrderedCollection<T>, List<T> and Meta<T> are only ever closed over reference " +
                        "types (the source skips value-type item requests); MakeGenericType over " +
                        "reference types is AOT-safe.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "All involved types live in DesomniaCore/Autofac, which the trimmer root descriptors preserve whole.")]
    [UnconditionalSuppressMessage("Trimming", "IL2062",
        Justification = "All involved types live in DesomniaCore/Autofac, which the trimmer root descriptors preserve whole.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067",
        Justification = "All involved types live in DesomniaCore/Autofac, which the trimmer root descriptors preserve whole.")]
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "All involved types live in DesomniaCore/Autofac, which the trimmer root descriptors preserve whole.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "All involved types live in DesomniaCore/Autofac, which the trimmer root descriptors preserve whole.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "All involved types live in DesomniaCore/Autofac, which the trimmer root descriptors preserve whole.")]
    internal sealed class OrderedCollectionSource : IRegistrationSource
    {
        public bool IsAdapterForIndividualComponents => false;

        public IEnumerable<IComponentRegistration> RegistrationsFor(
            Service service, Func<Service, IEnumerable<ServiceRegistration>> registrationAccessor)
        {
            // Only handle IOrderedCollection<T> requests.
            if (service is not IServiceWithType typed) yield break;

            var collectionType = typed.ServiceType;
            if (!collectionType.IsGenericType || collectionType.GetGenericTypeDefinition() != typeof(IOrderedCollection<>))
                yield break;

            var itemType = collectionType.GetGenericArguments()[0];
            if (itemType.IsValueType) // no shared native code under AOT — better an unresolved service than a runtime ILC miss
                yield break;

            var looseMeta   = typeof(Meta<>).MakeGenericType(itemType);
            var metaService = typed.ChangeType(typeof(IEnumerable<>).MakeGenericType(looseMeta)); // keeps a service key intact
            var resultType  = typeof(OrderedCollection<>).MakeGenericType(itemType);
            var itemList    = typeof(List<>).MakeGenericType(itemType);

            var valueProperty    = looseMeta.GetProperty(nameof(Meta<object>.Value))!;
            var metadataProperty = looseMeta.GetProperty(nameof(Meta<object>.Metadata))!;

            var registration = RegistrationBuilder.ForDelegate(collectionType, (context, parameters) =>
            {
                var metas = new List<(int Order, object? Value)>();

                foreach (var meta in (IEnumerable) context.ResolveService(metaService, parameters))
                {
                    var metadata = (IDictionary<string, object?>) metadataProperty.GetValue(meta)!;

                    var order = metadata.TryGetValue(OrderedRegistrationExtensions.OrderKey, out var value)
                                    && value is int position ? position : 0;

                    metas.Add((order, valueProperty.GetValue(meta)));
                }

                var items = (IList) Activator.CreateInstance(itemList)!;

                foreach (var (_, item) in metas.OrderBy(meta => meta.Order)) // stable sort
                    items.Add(item);

                return Activator.CreateInstance(resultType, items)!;
            }).As(service);

            yield return registration.CreateRegistration();
        }
    }
}
