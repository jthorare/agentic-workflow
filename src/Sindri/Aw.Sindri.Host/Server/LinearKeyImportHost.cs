using Aw.Sindri.Core.Lease;
using Aw.Sindri.Host.IndexSources;

namespace Aw.Sindri.Host.Server;

/// <summary>The serve-start part of the Linear key import (#3364): one <see cref="LinearKeyImport"/> pass, only while this process holds the lease, retried after 1 s doubling to 5 min until it answers, then done for the life of the process.</summary>
internal sealed partial class LinearKeyImportHost(IServiceScopeFactory scopes, HeldLease held, TimeProvider clock, ILogger<LinearKeyImportHost> log) : BackgroundService
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
                if (held.Current is null)
                {
                    await Task.Delay(_first, clock, stoppingToken).ConfigureAwait(false);   // why: no lease yet is not a failure
                    continue;
                }

                var scope = scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    var imported = await scope.ServiceProvider.GetRequiredService<LinearKeyImport>().Run(stoppingToken).ConfigureAwait(false);
                    if (imported.IsSuccess)
                    {
                        if (imported.Value)
                        {
                            LogImported(log);
                        }

                        return;
                    }

                    LogBackedOff(log, imported.Error.Code, imported.Error.Message, (int)wait.TotalSeconds);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a broken pass must not stop the process; the next one tries again
            {
                LogFailed(log, e.GetType().Name, e.Message);
            }

            await Task.Delay(wait, clock, stoppingToken).ConfigureAwait(false);
            wait = wait * 2 > _longest ? _longest : wait * 2;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The Linear key from the old keychain entry became the connection linear:default")]
    private static partial void LogImported(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Linear key import failed ({Code}: {Detail}); trying again in {Seconds} s")]
    private static partial void LogBackedOff(ILogger logger, string code, string detail, int seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Linear key import threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
