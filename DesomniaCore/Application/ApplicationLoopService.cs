using MadWizard.Desomnia.Environments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia
{
    /// <summary>
    /// Owns the configuration rebuild loop, inside the persistent host. Registered as the
    /// persistent host's one hosted service, so when the host stops, the loop's
    /// <paramref name="stoppingToken"/> is cancelled, the current inner host shuts down
    /// gracefully, and only then does the host dispose its container — restoring the OS state the
    /// persistent singletons hold before it reports stopped.
    /// <para>Each iteration builds a fresh inner application host and runs it until the
    /// <see cref="EnvironmentMonitor"/>'s reload signal (a configuration file change, or a
    /// condition change) or the stopping token fires. A reload re-enters the loop in-process — it
    /// never touches the persistent lifetime, so a Windows service reconfiguration no longer stops
    /// the service. When the inner host exits WITHOUT a reload (a fatal configuration error, or a
    /// deliberate stop such as the promiscuous-mode mutex), the loop stops the whole process.
    /// A rebuild that fails on a broken or half-written edit keeps the service running and waits for
    /// the next change; only the very first build must succeed.</para>
    /// </summary>
    internal sealed class ApplicationLoopService(
        ApplicationBuilder builder,
        EnvironmentMonitor environment,
        IHostApplicationLifetime lifetime,
        IApplicationFailureHandler failure,
        ILogger<ApplicationLoopService> logger) : BackgroundService
    {
        // after a failed rebuild, wake at least this often to retry, so a transient failure (a file
        // still being written) recovers even if no further watcher event arrives
        private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(3);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bool firstBuild = true;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    IHost host;
                    try
                    {
                        // BuildApplication arms the monitor's reload token for this run (via
                        // InjectInto), so a change that lands even during the build cancels the
                        // token below
                        host = builder.BuildApplication();
                    }
                    catch (Exception ex) when (!firstBuild && !stoppingToken.IsCancellationRequested)
                    {
                        // a rebuild (auto-reload / roam) hit an invalid or half-written configuration:
                        // keep the service running and wait for the next change, rather than stopping
                        // it. The initial build, by contrast, must succeed — there is nothing to fall
                        // back to, so it falls through to the fatal handler.
                        logger.LogError(ex, "Rebuilding from the changed configuration failed; keeping the service running until the next change.");

                        if (await WaitForReload(stoppingToken))
                            continue;

                        break;
                    }

                    firstBuild = false;

                    bool reloadRequested;

                    using (host) using (var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, environment.ReloadToken))
                    {
                        try
                        {
                            if (!linked.IsCancellationRequested)
                                await host.RunAsync(linked.Token);
                        }
                        catch (OperationCanceledException) when (linked.IsCancellationRequested)
                        {
                            // the normal end of a run: a reload or stop, possibly during startup
                        }
                        catch (Exception ex) when (linked.IsCancellationRequested)
                        {
                            // the run was ending anyway (reload or process stop) and the inner host
                            // faulted while draining — e.g. a plugin's IHostedService.StopAsync threw.
                            // Never fatal: a reload rebuilds, a stop exits.
                            logger.LogWarning(ex, "The application host faulted while stopping for a reload or shutdown; continuing.");
                        }

                        // capture BEFORE the inner container disposal widens the window: an in-flight
                        // change on the watcher thread must not turn a deliberate inner stop into a
                        // rebuild
                        reloadRequested = environment.ReloadToken.IsCancellationRequested;
                    }

                    // a deliberate inner stop (a fatal runtime configuration, the promiscuous-mode
                    // mutex) leaves the token uncancelled and ends the loop -> the whole process stops
                    if (!reloadRequested)
                        break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // the first build failed, or the inner host faulted at runtime with no reload pending
                logger.LogCritical(ex, "The application loop terminated unexpectedly.");

                failure.OnFatal(ex);
            }
            finally
            {
                // an inner host that ended without asking for a reload (fatal configuration,
                // promiscuous-mode mutex, ...) must bring the whole process down; a stopping
                // token means the persistent host is already stopping, so leave it be
                if (!stoppingToken.IsCancellationRequested)
                    lifetime.StopApplication();
            }
        }

        /// <summary>Waits for the next reload signal (or the process stop), waking periodically to
        /// retry a transient failure. Returns false when the loop should end.</summary>
        private async Task<bool> WaitForReload(CancellationToken stoppingToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, environment.ReloadToken);

            try
            {
                await Task.Delay(RetryInterval, linked.Token);
            }
            catch (OperationCanceledException)
            {
                // a change arrived, or the process is stopping
            }

            return !stoppingToken.IsCancellationRequested;
        }
    }
}
