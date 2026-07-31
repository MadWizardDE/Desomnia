namespace MadWizard.Desomnia.Configuration
{
    /// <summary>
    /// The command side of an action attribute — today a single function invocation with
    /// an optional parameter list (<c>notify('hello','world')</c>). Deliberately its own
    /// type: richer expressions (arbitrary JavaScript, some day) extend HERE without
    /// touching <see cref="ActionInfo"/> or the dispatch seams again.
    /// </summary>
    public class CommandExpression(string function, Arguments? args = null)
    {
        public string Function => function;

        public Arguments? Arguments => args;

        public override string ToString() => function + (args?.ToString() ?? "");
    }
}
