using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Ends the graph runs left 'running' with no Temporal workflow. The first pass ends the previous process's runs (issue 2367): one <see cref="IGraphRunRecovery"/> attempt each second, only while this process holds the lease,
/// until it succeeds. Then the sweep (issue 2999) repeats every minute for the life of the process, over every run older than five minutes (this process's own included), so a run whose workflow closed
/// without writing its end cannot hold its workflow's slot until the next restart.
/// </summary>
internal sealed partial class GraphRunRecoveryHost(IGraphRunRecovery recovery, HeldLease held, TimeProvider clock, ILogger<GraphRunRecoveryHost> log) : BackgroundService
{
    private static readonly TimeSpan _retry = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _sweep = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan _margin = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset? tookLease = null;
        var swept = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (held.Current is not null)
                {
                    // why: a trigger can admit a run in the window after the lease is taken; only runs from before it can be orphans of the previous process
                    tookLease ??= clock.GetUtcNow();
                    // why: the first pass leaves anything admitted after the lease alone; later passes use an age margin for "still launching" (the starter also spares a launch in flight)
                    var ended = await recovery.Run(swept ? clock.GetUtcNow() - _margin : tookLease.Value, stoppingToken).ConfigureAwait(false);
                    if (ended.IsSuccess)
                    {
                        if (ended.Value > 0)
                        {
                            LogRecovered(log, ended.Value);
                        }

                        swept = true;
                        await Task.Delay(_sweep, clock, stoppingToken).ConfigureAwait(false);
                        continue;
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

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} graph run(s) left running with no Temporal workflow were ended as failed (lost)")]
    private static partial void LogRecovered(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Graph run recovery threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
