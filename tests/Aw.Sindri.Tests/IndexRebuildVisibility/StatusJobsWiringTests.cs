using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.IndexRebuildVisibility;

/// <summary>Issue #2945: the host's container hands the real jobs view to the status health rows, so status never silently lists no jobs.</summary>
public sealed class StatusJobsWiringTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task The_host_gives_the_status_health_rows_the_jobs_view()
    {
        await using var app = LoopbackBinding.Build([], n => n == "AW_SINDRI_HOME" ? _home.Path : null, TimeProvider.System, NoTemporal.Use);
        await using var scope = app.Services.CreateAsyncScope();

        var rows = scope.ServiceProvider.GetRequiredService<HealthServiceRows>();

        Assert.True(rows.ReadsJobs());
    }
}
