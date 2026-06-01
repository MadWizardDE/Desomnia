using System.Net;

namespace MadWizard.Desomnia.Network.Manager
{
    public interface IStaticNameMapping
    {
        public void Insert(string name, IPAddress ip);
        public void Delete(string name);
    }
}
