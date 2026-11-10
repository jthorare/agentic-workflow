using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// The serve-start part of the run-item repair (issue 2588): one <see cref="IRunItemRepair.RepairAll"/> attempt each second, only while this process holds the lease, until it succeeds;
/// then it is done for the life of the process. A run that ends later is repaired by its own finish.
/// </summary>
internal sealed partial class RunItemRepairHost(IRunItemRepair repair, HeldLease held, TimeProvider clock, ILogger<RunItemRepairHost> log) : BackgroundService
{
    private static readonly TimeSpan _retry = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} missing run link(s) or run item(s) of create_item nodes were written")]
    private static partial void LogRepaired(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The run item repair threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
