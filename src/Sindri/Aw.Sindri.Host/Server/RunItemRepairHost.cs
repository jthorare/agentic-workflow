using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// The serve-start part of the run-item repair (issue 2588): one <see cref="IRunItemRepair.RepairAll"/> attempt, only while this process holds the lease, until it succeeds (a failed one is logged and tried again after 1 s, doubling to 5 min);
/// then it is done for the life of the process. A run that ends later is repaired by its own finish.
/// </summary>
internal sealed partial class RunItemRepairHost(IRunItemRepair repair, HeldLease held, TimeProvider clock, ILogger<RunItemRepairHost> log) : BackgroundService
{
    private static readonly TimeSpan _first = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _longest = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = _first;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (held.Current is not null)
                {
                    var repaired = await repair.RepairAll(stoppingToken).ConfigureAwait(false);
                    if (repaired.IsSuccess)
                    {
                        if (repaired.Value > 0)
                        {
                            LogRepaired(log, repaired.Value);
                        }

                        return;
                    }

                    LogBackedOff(log, repaired.Error.Code, repaired.Error.Message, (int)wait.TotalSeconds);
                    await Task.Delay(wait, clock, stoppingToken).ConfigureAwait(false);
                    wait = Longer(wait);
                    continue;
                }

                await Task.Delay(_first, clock, stoppingToken).ConfigureAwait(false);   // why: no lease yet is not a failure, so it waits the short time and does not back off
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a broken attempt must not stop the process; the next one tries again
            {
                LogFailed(log, e.GetType().Name, e.Message);
                await Task.Delay(wait, clock, stoppingToken).ConfigureAwait(false);
                wait = Longer(wait);
            }
        }
    }

    private static TimeSpan Longer(TimeSpan wait) => wait * 2 > _longest ? _longest : wait * 2;

    [LoggerMessage(Level = LogLevel.Warning, Message = "The run item repair failed ({Code}: {Detail}); trying again in {Seconds} s")]
    private static partial void LogBackedOff(ILogger logger, string code, string detail, int seconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} missing run link(s) or run item(s) of create_item nodes were written")]
    private static partial void LogRepaired(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The run item repair threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
