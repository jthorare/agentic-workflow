using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflows;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>
/// A control-plane read on a Temporal that died under a live client fails fast: SND-API-001 within a few seconds, not after the
/// SDK's default retry (about 5 s a call). Writes and signals keep the default retry (see <see cref="DeadTemporalReadsTests"/>).
/// It asserts on wall clock, so it runs alone within the assembly.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class DeadTemporalReadsFailFastTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(3);   // the default retry alone is about 5 s a call

    [Fact(Timeout = 120_000)]
    public async Task Each_read_on_a_Temporal_that_died_is_SND_API_001_within_the_bound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempHome();
        var environment = await WorkflowsHarness.StartDevServer(ct: ct);
        var settings = new TemporalSettings(home.Path, PinnedTemporal.Path());
        using var frontend = new TemporalFrontend(new DeadTemporalEndpoint(WorkflowsHarness.PortOf(environment)), settings);
        var known = new KnownInstances();
        var runtime = new TemporalInstanceRuntime(frontend, new TemporalDeskReports(frontend), new TemporalCommands(frontend), settings, known);
        var directory = new TemporalInstanceDirectory(frontend, known);
        var instances = new TemporalWorkflowInstances(frontend);
        Assert.Equal("SND-WF-001", (await runtime.Get(InstanceAddress.From("T-90/step/1"), ct)).Error.Code);   // connected
        await environment.ShutdownAsync();

        var reads = new (string Name, Func<ValueTask<string>> Read)[]
        {
            ("query", async () => (await runtime.Get(InstanceAddress.From("T-90/step/1"), ct)).Error.Code),
            ("describe (latest)", async () => (await directory.Latest(ItemKey.From("T-90"), RecipeId.From("workflow.step"), ct)).Error.Code),
            ("list (item)", async () => (await directory.Resolve("T-90", ct)).Error.Code),
            ("list (id)", async () => (await directory.Resolve(WorkflowInstanceId.Of(InstanceAddress.From("T-91/step/1")).Value, ct)).Error.Code),
            ("list (instances)", async () => (await instances.List(query: null, ended: false, ct)).Error.Code),
        };

        foreach (var (name, read) in reads)
        {
            var clock = Stopwatch.StartNew();
            var code = await read();
            Assert.True(clock.Elapsed < _bound, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{name} took {clock.Elapsed.TotalSeconds:0.0} s"));
            Assert.Equal("SND-API-001", code);
        }
    }
}
