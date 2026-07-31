using MadWizard.Desomnia.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Desomnia.Service.Duo.Configuration
{
    // No single-string constructor here: the strict binder reserves that signature
    // for XML text content, which this class does not accept.
    public class DuoInstanceInfo
    {
        public required string Name { get; set; }

        public ActionInfo? OnDemand { get; set; }

        public DelayedActionInfo? OnIdle { get; set; }

        public DelayedActionInfo? OnLogin { get; set; }
        public DelayedActionInfo? OnStart { get; set; }
        public DelayedActionInfo? OnStop { get; set; }
        public DelayedActionInfo? OnLogout { get; set; }

    }
}
