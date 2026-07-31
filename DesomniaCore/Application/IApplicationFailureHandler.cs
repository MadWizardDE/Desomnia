using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia
{
    /// <summary>
    /// Reports a fatal application failure (a configuration that fails to build or run) to
    /// the platform, so the supervisor can react. On the Windows service this must set a
    /// non-zero <c>ServiceBase.ExitCode</c> before the service reports STOPPED, so the SCM
    /// recovery actions fire; on the daemons it sets the process exit code. Called from the
    /// application loop when an inner-host build or run throws, before it stops the process.
    /// </summary>
    public interface IApplicationFailureHandler
    {
        void OnFatal(Exception exception);
    }

    /// <summary>Default handler: logs the failure and sets the process exit code. Correct for
    /// the console/daemon hosts (systemd/launchd read the process exit code); the Windows
    /// service overrides it to also set the SCM-visible <c>ServiceBase.ExitCode</c>.</summary>
    internal sealed class LoggingFailureHandler(ILogger<LoggingFailureHandler> logger) : IApplicationFailureHandler
    {
        public void OnFatal(Exception exception)
        {
            logger.LogCritical(exception, "The application terminated because of a fatal error.");

            Environment.ExitCode = 1;
        }
    }
}
