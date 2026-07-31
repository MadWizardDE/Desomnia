using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace MadWizard.Desomnia.Service
{
    public class WindowsService : WindowsServiceLifetime, IApplicationFailureHandler
    {
        // the SCM's managed stop wait (WindowsServiceLifetime._delayStop.Wait) is bounded by the
        // host's ShutdownTimeout; this only asks the SCM to keep waiting that long, so it does
        // not cut off the ordered teardown (inner host drain -> persistent container disposal)
        private static readonly TimeSpan ShutdownWaitHint = TimeSpan.FromSeconds(65);

        public required ILogger<WindowsService> Logger { private get; init; }

        /// <summary>The application this service maps to the SCM. Torn down by
        /// <see cref="ShutdownApplication"/> before a stop returns.</summary>
        public required ApplicationBuilder Application { private get; init; }

        public WindowsService(IHostEnvironment environment, IHostApplicationLifetime lifetime, ILoggerFactory logging, IOptions<HostOptions> options)
            : base(environment, lifetime, logging, options)
        {
            CanHandlePowerEvent = true;
            CanHandleSessionChangeEvent = true;
            CanShutdown = true;
        }

        /// <summary>Reports a fatal application failure to the SCM: a non-zero exit code makes
        /// the service stop count as a failure, so the installer's recovery actions fire.</summary>
        public void OnFatal(Exception exception)
        {
            ExitCode = 1; // ServiceBase.ExitCode -> reported as the win32 exit code with SERVICE_STOPPED

            Environment.ExitCode = 1;

            try
            {
                System.Diagnostics.EventLog.WriteEntry("Desomnia", $"{exception}", EventLogEntryType.Error);
            }
            catch
            {
                // best effort - the application loop already logged it via NLog
            }
        }

        internal event EventHandler<PowerBroadcastStatus>? PowerStatusChanged;
        internal event EventHandler<SessionChangeDescription>? SessionChanged;

        protected override void OnStart(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith("/WaitForDebugger"))
                {
                    Helper.Debugger.UntilAttached().Wait();
                }
            }

            base.OnStart(args);

            Logger.LogInformation("Startup complete");
        }

        protected override void OnStop()
        {
            Logger.LogInformation("Shutdown requested...");

            // draining the inner host happens inside base.OnStop's wait; ask the SCM to be
            // patient for that and for the teardown that follows it
            RequestAdditionalTime(ShutdownWaitHint);

            base.OnStop();

            ShutdownApplication();

            Logger.LogInformation("Shutdown complete");
        }

        protected override void OnShutdown()
        {
            Logger.LogInformation("System is shutting down - stopping...");

            // the OS caps this far more tightly than a normal stop (WaitToKillServiceTimeout),
            // but ask anyway; the teardown restores the OS state in its own order of value
            RequestAdditionalTime(ShutdownWaitHint);

            base.OnShutdown();

            ShutdownApplication();

            Logger.LogInformation("Shutdown complete");
        }

        /// <summary>
        /// Tears the application down while the service is still alive and the SCM is still
        /// waiting — the last thing a stop does before it returns.
        /// <para>Returning from <see cref="OnStop"/>/<see cref="OnShutdown"/> ends the SCM
        /// dispatcher, and <c>ServiceBase.Run</c> disposes THIS instance in its own <c>finally</c>
        /// the moment it does. That happens outside the container, on the dispatcher's thread, so
        /// no registration order can put anything after it: whatever the persistent singletons do
        /// at disposal (re-enabling interfaces, reconnecting displays) has to have happened by the
        /// time we return. Disposing the host here is what guarantees that — and it is also what
        /// keeps the restore ahead of SERVICE_STOPPED.</para>
        /// </summary>
        private void ShutdownApplication()
        {
            // the teardown gets its own slice of the SCM's patience: the wait inside base.OnStop
            // may already have spent most of the hint asked for when the stop began
            try
            {
                RequestAdditionalTime(ShutdownWaitHint);
            }
            catch
            {
                // best effort - the hint only ever buys time, never spends it
            }

            try
            {
                Application.Dispose();
            }
            catch (Exception ex)
            {
                // never let a teardown failure keep the service from reporting stopped
                Logger.LogError(ex, "Failed to shut the application down cleanly.");
            }
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            PowerStatusChanged?.Invoke(this, powerStatus);

            return true; // TODO Query Suspended?
        }
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            SessionChanged?.Invoke(this, changeDescription);
        }

        internal void ScheduleSelfRestart()
        {
            var ps = $@"
                do {{
                    Start-Sleep -Seconds 1
                    $s = Get-Service -Name {this.ServiceName} -ErrorAction SilentlyContinue
                }} while ($s -and $s.Status -ne 'Stopped')

                Start-Service -Name {this.ServiceName}
            ";

            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{ps}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        #region ServiceStatus
        private void ReportServiceStatus(ServiceState state, TimeSpan waitHint = default)
        {
            ServiceStatus status = new()
            {
                dwCurrentState = state,
                dwWaitHint = (int)waitHint.TotalMilliseconds
            };

            SetServiceStatus(this.ServiceHandle, ref status);
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(nint handle, ref ServiceStatus serviceStatus);

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        }

        private enum ServiceState
        {
            SERVICE_STOPPED = 1,
            SERVICE_START_PENDING = 2,
            SERVICE_STOP_PENDING = 3,
            SERVICE_RUNNING = 4,
            SERVICE_CONTINUE_PENDING = 5,
            SERVICE_PAUSE_PENDING = 6,
            SERVICE_PAUSED = 7,
        }
        #endregion
    }
}