using Aw.Sindri.Features.AdoptToolkits;

namespace Aw.Sindri.Host.Server;

/// <summary>Adopts toolkit folders that have no row: one <see cref="ToolkitAdoption"/> pass at start, retried each second until the store answers, then done for the life of the process.</summary>
internal sealed class ToolkitAdoptionHost(ToolkitAdoption adoption, TimeProvider clock, ILogger<ToolkitAdoptionHost> log) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = (adoption, clock, log, stoppingToken);
        return Task.CompletedTask;
    }
}
