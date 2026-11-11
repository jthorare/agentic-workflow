using Aw.Sindri.Features.AdoptToolkits;

namespace Aw.Sindri.Host.Server;

/// <summary>Adopts toolkit folders that have no row: one <see cref="ToolkitAdoption"/> pass at start, retried with a backoff (1 s doubling to 60 s) until the store answers, then done for the life of the process. A failed pass is logged once per kind of failure, not every retry.</summary>
internal sealed partial class ToolkitAdoptionHost(ToolkitAdoption adoption, TimeProvider clock, ILogger<ToolkitAdoptionHost> log) : BackgroundService
{
    private static readonly TimeSpan _first = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _cap = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = _first;
        string? lastFailure = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var failure = await Once(stoppingToken).ConfigureAwait(false);
                if (failure is null)
                {
                    return;
                }

                if (!string.Equals(failure.Value.Key, lastFailure, StringComparison.Ordinal))
                {
                    lastFailure = failure.Value.Key;
                    failure.Value.Log();
                }

                await Task.Delay(wait, clock, stoppingToken).ConfigureAwait(false);
                wait = NextWait(wait);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>One adoption pass: null when it is done, else the failure's key (its code or exception type) and how to log it.</summary>
    private async Task<(string Key, Action Log)?> Once(CancellationToken stoppingToken)
    {
        try
        {
            var adopted = await adoption.Run(stoppingToken).ConfigureAwait(false);
            if (adopted.IsError)
            {
                var error = adopted.Error;
                return (error.Code, () => LogRefused(log, error.Code, error.Message));
            }

            if (adopted.Value > 0)
            {
                LogAdopted(log, adopted.Value);
            }

            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException || !stoppingToken.IsCancellationRequested)   // boundary: a broken pass must not stop the process; the next one tries again
        {
            var type = e.GetType().Name;
            return (type, () => LogFailed(log, type, e.Message));
        }
    }

    /// <summary>The wait after <paramref name="wait"/>: double it, never past 60 s.</summary>
    /// <param name="wait">The wait just used.</param>
    /// <returns>The next wait.</returns>
    internal static TimeSpan NextWait(TimeSpan wait) => wait * 2 > _cap ? _cap : wait * 2;

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} toolkit folder(s) with no row were adopted")]
    private static partial void LogAdopted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Toolkit adoption was refused with {Code}: {Detail}; trying again with a backoff")]
    private static partial void LogRefused(ILogger logger, string code, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Toolkit adoption threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
