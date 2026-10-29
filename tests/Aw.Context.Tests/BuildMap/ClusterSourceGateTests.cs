using System.Reflection;
using Aw.Context.Core.Extract;
using Aw.Context.Features;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Every graphify run on a host goes through <see cref="MapBuildGate"/> (#1683): one at a time, and a caller that waits says whom it waits for.</summary>
public sealed class ClusterSourceGateTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_feature_that_runs_the_cluster_source_takes_the_map_build_gate()
    {
        var offenders = typeof(MapBuildGate).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IClusterSource)) && !c.GetParameters().Any(p => p.ParameterType == typeof(MapBuildGate))))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, $"These take an IClusterSource without the MapBuildGate: {string.Join(", ", offenders)}");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_enter_waits_and_names_the_holder_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var gate = new MapBuildGate();
        var progress = new SyncProgress();
        var first = await gate.Enter("JOB-A", progress: null, ct);

        var second = gate.Enter("JOB-B", progress, ct);

        Assert.False(second.IsCompleted);
        Assert.Equal(["waiting for the map build of job JOB-A"], progress.Lines);
        first.Dispose();
        (await second).Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_enter_with_no_one_holding_reports_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var gate = new MapBuildGate();
        var progress = new SyncProgress();

        (await gate.Enter("JOB-A", progress, ct)).Dispose();

        Assert.Empty(progress.Lines);
    }
}
