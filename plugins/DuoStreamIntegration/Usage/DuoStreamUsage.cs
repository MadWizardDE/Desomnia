namespace MadWizard.Desomnia.Service.Duo
{
    internal class DuoStreamUsage(string name) : UsageToken
    {
        public string Name => name;

        public override string ToString() => $"DuoStream<{name}>";
    }
}
