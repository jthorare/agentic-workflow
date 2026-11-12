using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Keeps each agent's spend for the local day warm (#3213): shortly after serve starts and every 30 s after, one <see cref="ILaunchSpend.Today"/> read for each
/// catalog entry that names a usage reader, so no request waits on the first walk of the transcript folders.
/// </summary>
#pragma warning disable MA0182 // registered by the fix for #3213; the red commit only has the test using it
internal sealed class SpendWarmHost(IAgentCatalog catalog, ILaunchSpend spend, TimeProvider clock, ILogger<SpendWarmHost> log) : BackgroundService
{
    private readonly IAgentCatalog _catalog = catalog;
    private readonly ILaunchSpend _spend = spend;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<SpendWarmHost> _log = log;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _catalog is null || _spend is null || _clock is null || _log is null ? throw new ArgumentNullException(nameof(stoppingToken)) : throw new NotSupportedException("#3213: not built yet");
}
#pragma warning restore MA0182
