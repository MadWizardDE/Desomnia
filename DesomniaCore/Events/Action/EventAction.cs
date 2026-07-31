using MadWizard.Desomnia.Configuration;

namespace MadWizard.Desomnia.Events
{
    /// <summary>
    /// The engine's action model: the WHEN (scheduling facet on this base) is decoupled
    /// from the HOW (the subclasses). Configuration types (<see cref="ActionInfo"/> and
    /// descendants) are converted at wiring time — the engine never executes config
    /// objects directly.
    /// </summary>
    public abstract class EventAction
    {
        /// <summary>"+10min" — arm on first trigger, fire after the delay (first event wins).</summary>
        public TimeSpan? Delay { get; init; }

        /// <summary>"+2x" — fire after N further triggers (the arming trigger does not count).</summary>
        public uint? Times { get; init; }

        public bool HasSchedule => Delay is not null || Times is not null;

        /// <summary>Converts a configuration action into the engine model — URL vs JS is
        /// decided by the structure the converter detected (§6.4). Zero delay/times
        /// (the "+0s"/"+0x" converter defaults) mean no schedule.</summary>
        public static EventAction? FromConfig(ActionInfo? info)
        {
            if (info == null)
                return null;

            TimeSpan? delay = info is ScheduledActionInfo scheduled && scheduled.Delay > TimeSpan.Zero ? scheduled.Delay : null;
            uint? times = info is ThrottledActionInfo throttled && throttled.Times > 0 ? throttled.Times : null;

            if (info.URL is Uri url)
                return new URLEventAction(url) { Delay = delay, Times = times };

            if (info.Command is not CommandExpression command || command.Function.Trim() == string.Empty)
                return null;                                  // unset XML attribute (blank-command no-op)

            object[]? args = null;
            if (command.Arguments is Arguments arguments)
            {
                args = new object[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                    args[i] = arguments[i];
            }

            return new JSEventAction(command.Function, args) { Delay = delay, Times = times };
        }

    }

    /// <summary>A named "JS-style" invocation — name('arg1','arg2') — resolved against
    /// [ActionHandler] registries.</summary>
    public sealed class JSEventAction(string name, IReadOnlyList<object>? arguments = null) : EventAction
    {
        public string Name => name;

        public IReadOnlyList<object>? Arguments => arguments;

        public override string ToString() => name + (arguments is { Count: > 0 } a ? $"({string.Join(",", a)})" : "");
    }

    /// <summary>A scheme-addressed interaction — resolved against [URLActionHandler]
    /// registries (§6.4): the nearest node up the tree declaring the scheme wins, the
    /// root providers come last. Never confused with JS-style name resolution.</summary>
    public sealed class URLEventAction(Uri url) : EventAction
    {
        public Uri Url => url;

        public override string ToString() => url.OriginalString;
    }
}
