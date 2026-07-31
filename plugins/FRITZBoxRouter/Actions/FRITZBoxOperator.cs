using MadWizard.Desomnia.Events;
using MadWizard.Desomnia.Network.FRITZ.Neighborhood;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.FRITZ.Actions
{
    /// <summary>
    /// The <c>fritz</c> URL action provider (§6.4): registered at the root, so every
    /// <c>fritz://</c> URL action resolves here regardless of which monitor raised it —
    /// e.g. a <see cref="MadWizard.Desomnia.Processes"/> firing
    /// <c>onStart="fritz://heimdail/ports/eth0?maxspeed=1000"</c> when Moonlight launches.
    ///
    /// <para>The addressed box is looked up by name across the running networks' segments
    /// (<see cref="FRITZBoxRouter"/> routers live in the <c>NetworkSegment</c> like any other host).
    /// The monitor list is resolved per action, because networks come and go with their
    /// interfaces.</para>
    ///
    /// <para>A malformed <c>fritz://</c> URL no longer falls through to name resolution
    /// (release-noted, spec §9.3) — it fails descriptively through the error chain.</para>
    /// </summary>
    public sealed class FRITZBoxOperator : ActionProvider
    {
        public required ILogger<FRITZBoxOperator> Logger { private get; init; }

        public required Lazy<IEnumerable<NetworkMonitor>> Monitors { private get; init; }

        [URLActionHandler(FritzActionURL.Scheme)]
        public async Task HandleFritzAction(Uri url)
        {
            if (!FritzActionURL.TryParse(url, out var uri) || uri is null)
                throw new ArgumentException(
                    $"{url}: not a well-formed fritz action — expected fritz://<box>/<resource>/<id>?<prop>=<value>");

            await DispatchAsync(uri);
        }

        private async Task DispatchAsync(FritzActionURL uri)
        {
            var box = FindRouter(uri.Box)
                ?? throw new InvalidOperationException(
                    $"{uri}: no <FRITZBoxRouter name=\"{uri.Box}\"> is up on any monitored network.");

            switch (uri.Resource.ToLowerInvariant())
            {
                case "ports":
                    await ApplyPortAsync(box, uri);
                    break;

                default:
                    throw new NotSupportedException($"{uri}: unknown resource '{uri.Resource}'.");
            }
        }

        private FRITZBoxRouter? FindRouter(string name)
        {
            foreach (var monitor in Monitors.Value)
            {
                if (monitor.Network[name] is FRITZBoxRouter box)
                    return box;
            }

            return null;
        }

        private async Task ApplyPortAsync(FRITZBoxRouter box, FritzActionURL uri)
        {
            if (!uri.Properties.TryGetValue("maxspeed", out var raw) || !int.TryParse(raw, out var maxSpeed))
                throw new ArgumentException($"{uri}: a numeric ?maxspeed= is required.");

            var port = await box.ResolvePortAsync(uri.Id)
                ?? throw new InvalidOperationException($"{uri}: box '{box.Name}' has no port '{uri.Id}'.");

            uri.Properties.TryGetValue("eee_mode", out var eeeMode);

            await box.SetPortMaxSpeedAsync(port, maxSpeed, eeeMode);

            Logger.LogInformation("FRITZ!Box \"{Box}\" – <{Port}> maxspeed = {Speed} Mbit/s",
                box.Name, port.Label, maxSpeed);
        }
    }
}
