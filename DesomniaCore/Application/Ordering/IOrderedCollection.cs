using System.Collections;
using System.Runtime.CompilerServices;

namespace MadWizard.Desomnia
{
    /// <summary>
    /// Relationship type for resolving an ordered service collection: like <see cref="IEnumerable{T}"/>,
    /// but sorted by the order metadata attached to the component registrations (see
    /// <see cref="OrderedRegistrationExtensions.WithOrder"/>). Components without order metadata count as
    /// order 0; components with equal order keep their registration order. Provided container-wide by
    /// <see cref="OrderedCollectionSource"/>; the standard relationship types
    /// (<see cref="IEnumerable{T}"/>, arrays, ...) remain untouched.
    /// </summary>
    [CollectionBuilder(typeof(OrderedCollection), nameof(OrderedCollection.Create))]
    public interface IOrderedCollection<out T> : IReadOnlyList<T>;

    public static class OrderedCollection
    {
        public static IOrderedCollection<T> Create<T>(ReadOnlySpan<T> items) => new OrderedCollection<T>([.. items]);
    }

    internal sealed class OrderedCollection<T>(IReadOnlyList<T> items) : IOrderedCollection<T>
    {
        public T this[int index] => items[index];

        public int Count => items.Count;

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
