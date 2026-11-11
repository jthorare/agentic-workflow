using Aw.Sindri.Features.StartToolkit;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Sets the toolkits the previous serve left 'running' or 'paused' back to 'built' (this serve has no processes): one <see cref="ToolkitRecovery"/> pass at start,
/// retried each second until the store answers, then done for the life of the process.
/// </summary>
internal sealed partial class ToolkitRecoveryHost(ToolkitRecovery recovery, TimeProvider clock, ILogger<ToolkitRecoveryHost> log) : BackgroundService
{
    private static readonly TimeSpan _retry = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var changed = await recovery.Run(stoppingToken).ConfigureAwait(false);
                if (changed.IsSuccess)
                {
                    if (changed.Value > 0)
                    {
                        LogReset(log, changed.Value);
                    }

                    return;
                }

                await Task.Delay(_retry, clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a broken pass must not stop the process; the next one tries again
            {
                LogFailed(log, e.GetType().Name, e.Message);
                await Task.Delay(_retry, clock, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} toolkit(s) the previous process left running were set back to built")]
    private static partial void LogReset(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Toolkit recovery threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
