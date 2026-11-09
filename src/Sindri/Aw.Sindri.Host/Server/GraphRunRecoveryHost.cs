using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Ends the graph runs the previous process left 'running' with no Temporal workflow (issue 2367): one <see cref="IGraphRunRecovery"/> attempt each second, only while this process holds the lease,
/// until it succeeds; then it is done for the life of the process.
/// </summary>
internal sealed partial class GraphRunRecoveryHost(IGraphRunRecovery recovery, HeldLease held, TimeProvider clock, ILogger<GraphRunRecoveryHost> log) : BackgroundService
{
    private static readonly TimeSpan _retry = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset? tookLease = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (held.Current is not null)
                {
                    // why: a trigger can admit a run in the window after the lease is taken; only runs from before it can be orphans of the previous process
                    tookLease ??= clock.GetUtcNow();
                    var ended = await recovery.Run(tookLease.Value, stoppingToken).ConfigureAwait(false);
                    if (ended.IsSuccess)
                    {
                        if (ended.Value > 0)
                        {
                            LogRecovered(log, ended.Value);
                        }

                        return;
                    }
                }

                await Task.Delay(_retry, clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a broken attempt must not stop the process; the next one tries again
            {
                LogFailed(log, e.GetType().Name, e.Message);
                await Task.Delay(_retry, clock, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} graph run(s) the previous process left running with no Temporal workflow were ended as failed (lost at restart)")]
    private static partial void LogRecovered(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Graph run recovery threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
