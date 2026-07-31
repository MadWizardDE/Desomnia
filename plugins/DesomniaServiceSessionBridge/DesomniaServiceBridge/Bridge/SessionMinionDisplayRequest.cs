using MadWizard.Desomnia.Pipe.Messages;
using MadWizard.Desomnia.Power.Manager;

namespace MadWizard.Desomnia.Service.Bridge
{
    /// <summary>
    /// A display power request held by a session minion on the service's behalf. Disposing sends
    /// the (idempotent) release message to the minion the request was placed with; if that minion
    /// has terminated in the meantime, its process death has already released the native request.
    /// </summary>
    internal sealed class SessionMinionDisplayRequest(Session session, string reason) : IPowerRequest
    {
        public string   Name    => "DesomniaSessionMinion";
        public string?  Reason  => reason;

        private bool _released;

        public void Dispose()
        {
            if (_released)
                return;

            _released = true;

            session.Minion?.Send(new DisplayRequestMessage());
        }

        public override string ToString()
        {
            return $"SessionMinionDisplayRequest(session={session.Id}, why='{reason}')";
        }
    }
}
