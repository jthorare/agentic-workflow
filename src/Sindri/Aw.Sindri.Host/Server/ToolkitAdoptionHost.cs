using Aw.Sindri.Features.AdoptToolkits;

namespace Aw.Sindri.Host.Server;

/// <summary>Adopts toolkit folders that have no row: one <see cref="ToolkitAdoption"/> pass at start, retried each second until the store answers, then done for the life of the process.</summary>
internal sealed partial class ToolkitAdoptionHost(ToolkitAdoption adoption, TimeProvider clock, ILogger<ToolkitAdoptionHost> log) : BackgroundService
{
    private static readonly TimeSpan _retry = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var adopted = await adoption.Run(stoppingToken).ConfigureAwait(false);
                if (adopted.IsSuccess)
                {
                    if (adopted.Value > 0)
                    {
                        LogAdopted(log, adopted.Value);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} toolkit folder(s) with no row were adopted")]
    private static partial void LogAdopted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Toolkit adoption threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
