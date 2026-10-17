using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Host.Server;

/// <summary>Samples what the live launched sessions have used every 60 s (SCR E-28): one private 'session.usage' per session whose tokens changed.</summary>
internal sealed partial class SessionUsageHost(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SessionUsageHost> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LaunchRules.UsageEvery, clock, stoppingToken).ConfigureAwait(false);
                var scope = scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    _ = await scope.ServiceProvider.GetRequiredService<ISessionUsageSampler>().Sample(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a broken sample must not stop the process; the next one tries again
            {
                LogFailed(log, e.GetType().Name, e.Message);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The session usage sample threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
