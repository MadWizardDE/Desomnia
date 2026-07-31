using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Desomnia.Pipe.Messages
{
    /// <summary>
    /// Instructs the minion to hold (<see cref="Active"/>) or release (default) a DISPLAY power
    /// request in its session — the session-0 service cannot issue those itself. Idempotent:
    /// re-sending the same state is safe, a hold replaces any previously held request.
    /// </summary>
    public class DisplayRequestMessage : UserMessage
    {
        public DisplayRequestMessage()
        {
        }

        public DisplayRequestMessage(string reason)
        {
            Active = true;
            Reason = reason;
        }

        public bool Active { get; set; }

        public string Reason { get; set; }
    }
}
