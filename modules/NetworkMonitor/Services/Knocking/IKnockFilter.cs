using System.Net;

namespace MadWizard.Desomnia.Network.Knocking
{
    public interface IKnockFilter
    {
        bool ShouldFilter(IPEndPoint source, KnockEvent knock);
    }

    internal class CompositeKnockFilter(IEnumerable<IKnockFilter> filters) : IKnockFilter
    {
        bool IKnockFilter.ShouldFilter(IPEndPoint source, KnockEvent knock)
        {
            foreach (IKnockFilter filter in filters)
            {
                if (filter.ShouldFilter(source, knock))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
