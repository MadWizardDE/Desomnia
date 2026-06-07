using Autofac;
using MadWizard.Desomnia.Service.Duo.Manager;
using MadWizard.Desomnia.Session;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Service.Duo
{
    internal class DuoStreamMonitor(DuoManager manager, SessionMonitor? sessionMonitor) : ResourceMonitor<DuoInstance>, IStartable
    {
        public required ILogger<DuoStreamMonitor> Logger { get; set; }

        void IStartable.Start()
        {
            sessionMonitor?.Filters += watch => !this.Any(instance => instance.HasInitiated(watch.Session));

            Logger.LogInformation($"Monitor is enabled. Waiting for service to start...");

            manager.Started += DuoService_Started;
            manager.Stopped += DuoService_Stopped;
        }

        private void DuoService_Started(object? sender, EventArgs e)
        {
            foreach (var instance in manager)
            {
                this.StartTracking(instance);
            }
        }

        private void DuoService_Stopped(object? sender, EventArgs e)
        {
            Logger.LogInformation($"Service has stopped. Monitoring will be suspended.");

            foreach (var instance in this)
            {
                this.StopTracking(instance);
            }
        }

        #region Instance Action Handlers
        [ActionHandler("start")]
        internal async Task HandleActionStart(DuoInstance instance)
        {
            if (await instance.Semaphore.WaitAsync(0))
            {
                try
                {
                    if (instance.IsRunning == false)
                        await manager.Start(instance);
                }
                finally
                {
                    instance.Semaphore.Release();
                }
            }
        }

        [ActionHandler("stop")]
        internal async Task HandleActionStop(DuoInstance instance)
        {
            if (await instance.Semaphore.WaitAsync(0))
            {
                try
                {
                    if (instance.IsRunning == true)
                        await manager.Stop(instance);
                }
                finally
                {
                    instance.Semaphore.Release();
                }
            }
        }
        #endregion
    }
}
