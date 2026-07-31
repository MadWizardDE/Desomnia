using MadWizard.Desomnia.Display.Manager;

namespace MadWizard.Desomnia.Display
{
    public class DisplayUsage(DisplayIdentity identity) : UsageToken
    {
        public DisplayIdentity Identity => identity;

        public DisplayUsage(IDisplay display) : this(display.Identity) { }

        public override string ToString()
        {
            string name = identity.Name ?? $"{identity.VendorId}:{identity.ProductCode:X4}";

            return $"<<{name}>>";
        }
    }
}
