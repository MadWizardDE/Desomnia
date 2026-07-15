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

        public NamedAction? OnDemand { get; set; }

        public DelayedAction? OnIdle { get; set; }

        public DelayedAction? OnLogin { get; set; }
        public DelayedAction? OnStart { get; set; }
        public DelayedAction? OnStop { get; set; }
        public DelayedAction? OnLogout { get; set; }

    }
}
