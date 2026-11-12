using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Keeps each agent's spend for the local day warm (#3213): shortly after serve starts and every 30 s after (which also covers local midnight), one
/// <see cref="ILaunchSpend.Today"/> read for each catalog entry that names a usage reader, so no request waits on the first walk of the transcript folders.
/// </summary>
internal sealed partial class SpendWarmHost(IAgentCatalog catalog, ILaunchSpend spend, TimeProvider clock, ILogger<SpendWarmHost> log) : BackgroundService
{
    private static readonly TimeSpan _first = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _every = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _stop = new();
    private Task? _firstWait;

    /// <summary>Creates the first wait on the clock before returning, so a clock moved right after start still fires it.</summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _firstWait = Task.Delay(_first, clock, _stop.Token);
        return base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _stop.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var registration = stoppingToken.Register(_stop.Cancel).ConfigureAwait(false);
        var first = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await (first && _firstWait is { } pending ? pending : Task.Delay(_every, clock, stoppingToken)).ConfigureAwait(false);
                first = false;
                var reads = catalog.All.Where(static e => e.Usage is { Length: > 0 } name && !string.Equals(name, "none", StringComparison.Ordinal)).Select(e => Warm(e.Id, stoppingToken));
                await Task.WhenAll(reads).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task Warm(AgentId id, CancellationToken cancellationToken)
    {
        try
        {
            _ = await spend.Today(id, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)   // boundary: a failed warm read must not stop the loop; the next pass tries again
        {
            LogFailed(log, id.Value, e.GetType().Name, e.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The spend warm read for {Agent} threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string agent, string type, string detail);
}
