using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Processes.Manager
{
    /// <summary>Uses ETW (Event Trace for Windows) to get process start/stop notifications in near realtime.</summary>
    public class TraceEventProcessManager : Win32ProcessManager, IDisposable
    {
        TraceEventSession? _traceEventSession;

        public TraceEventProcessManager()
        {
            this.ListenerCountChanged += (sender, @event) => ConfigureSession();
        }

        private bool IsProcessing => _traceEventSession?.IsActive ?? false;

        public override void Start() => ConfigureSession();

        private void ConfigureSession()
        {
            lock (this)
            {
                if (IsProcessing)
                {
                    if (ListenerCount == 0)
                    {
                        UnsubscribeFromTraceEvents();
                    }
                }
                else
                {
                    if (ListenerCount > 0)
                    {
                        SubscribeToTraceEvents();

                        RefreshProcessList();
                    }
                }
            }
        }

        private void SubscribeToTraceEvents()
        {
            Logger.LogDebug("Subscribing to process trace events...");

            _traceEventSession = new("Desomnia::ProcessManager");
            _traceEventSession.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);
            _traceEventSession.Source.Kernel.ProcessStart += ETW_ProcessStart;
            _traceEventSession.Source.Kernel.ProcessStop += ETW_ProcessStop;

            Task.Factory.StartNew(ETW_Process, TaskCreationOptions.LongRunning);
        }

        private void UnsubscribeFromTraceEvents()
        {
            if (_traceEventSession != null)
            {
                _traceEventSession.Source.StopProcessing();
                _traceEventSession.Stop();

                _traceEventSession.Dispose();
                _traceEventSession = null;

                Logger.LogDebug("Unsubscribed from process trace events");
            }
        }

        #region ETW callbacks
        private void ETW_Process()
        {
            try
            {
                _traceEventSession!.Source.Process();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ETW_Process"); // TODO maybe try to restart processing?
            }
        }

        private void ETW_ProcessStart(ProcessTraceData data)
        {
            try
            {
                TriggerStart(new(data.ProcessID)
                {
                    Name = data.ProcessName,
                    ParentId = data.ParentID,
                    SessionId = data.SessionID,
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ETW_ProcessStart");
            }
        }
        private void ETW_ProcessStop(ProcessTraceData data)
        {
            try
            {
                TriggerStop(data.ProcessID);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ETW_ProcessStop");
            }
        }
        #endregion

        public override IEnumerator<IProcess> GetEnumerator()
        {
            if (!IsProcessing)
                RefreshProcessList();

            return base.GetEnumerator();
        }

        public void Dispose()
        {
            Logger.LogDebug("Shutting down...");

            UnsubscribeFromTraceEvents();
        }
    }
}
