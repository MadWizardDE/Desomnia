using Autofac;
using MadWizard.Desomnia.Power.Manager;
using MadWizard.Desomnia.PowerRequest.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MadWizard.Desomnia.PowerRequest
{
    public class PowerRequestMonitor(IPowerManager power) : IInspectable, IStartable
    {
        public required ILogger<PowerRequestMonitor> Logger { get; set; }

        public required IEnumerable<PowerRequestFilterRule> Rules { private get; init; }

        void IStartable.Start()
        {
            Logger.LogDebug($"Startup complete; {Rules.Count()} filter rules found.");
        }

        IEnumerable<UsageToken> IInspectable.Inspect(TimeSpan interval)
        {
            var filteredRequests = power.ToBlockingEnumerable().Where(ShouldMonitorRequest);

            if (Rules.Where(rule => rule.Type == FilterRuleType.Must) is var mustRules && mustRules.Any())
            {
                foreach (var request in filteredRequests)
                    foreach (var rule in mustRules)
                        if (Matches(request, rule.Pattern))
                            yield return new PowerRequestToken(rule.Name);
            }
            else if (filteredRequests.Any()) // if there aren't any requests configured, any power request will match
            {
                yield return new PowerRequestToken();
            }
        }

        private bool ShouldMonitorRequest(IPowerRequest request)
        {
            foreach (var filter in Rules.Where(rule => rule.Type == FilterRuleType.MustNot))
                if (Matches(request, filter.Pattern))
                    return false;

            return true;
        }

        private static bool Matches(IPowerRequest request, Regex pattern)
        {
            if (pattern.Matches(request.Name).Count > 0)
                return true;
            if (request.Reason != null && pattern.Matches(request.Reason).Count > 0)
                return true;

            return false;
        }
    }
}
