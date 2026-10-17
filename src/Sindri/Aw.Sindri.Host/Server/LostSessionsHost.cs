using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Ends the launched sessions that went silent (API spec §8.1): every 10 s, a session with no heartbeat for 120 s gets 'session.ended' with reason lost, so a closed
/// tab or a killed wrap clears the double-launch guard. At start it also removes the capture rules and id files a dead launch left (older than 60 s; by then
/// 'session wrap' has read them, so a running session loses nothing).
/// </summary>
internal sealed partial class LostSessionsHost(IServiceScopeFactory scopes, TimeProvider clock, ILogger<LostSessionsHost> log) : BackgroundService
{
    private static readonly TimeSpan _every = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _stale = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var filesSwept = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_every, clock, stoppingToken).ConfigureAwait(false);
                var scope = scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    var lost = scope.ServiceProvider.GetRequiredService<ILostSessions>();
                    filesSwept = filesSwept || await lost.SweepFiles(_stale, stoppingToken).ConfigureAwait(false) is not null;   // once, as soon as the views know which sessions are live
                    var ended = await lost.Sweep(stoppingToken).ConfigureAwait(false);
                    if (ended > 0)
                    {
                        LogLost(log, ended);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a broken sweep must not stop the process; the next one tries again
            {
                LogFailed(log, e.GetType().Name, e.Message);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} launched session(s) went silent for 120 s and were ended as lost")]
    private static partial void LogLost(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The silent-session sweep threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
