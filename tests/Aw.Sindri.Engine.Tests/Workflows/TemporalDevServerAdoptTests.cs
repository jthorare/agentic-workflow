using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Engine P4a acceptance check 1 (engine spec rev 8.1, "Phase 4a"): a start finds the server already on the home's database:
/// it adopts one that answers, replaces one that doesn't, and a restart keeps the namespace and the search attributes.
/// Each test starts the real pinned 'temporal' CLI (binaries.lock); without it the test fails and says how to install it, so a
/// missing binary never reads as a pass. The tests are split across three classes so they run in parallel: each starts and stops
/// its own server on its own home (#1546).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalDevServerAdoptTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 60_000)]
    public async Task A_server_left_by_a_serve_that_died_is_adopted_by_the_next_start()
    {
        var ct = TestContext.Current.CancellationToken;
        var start = new TemporalServerStart(PinnedTemporal.Path(), _home.Path);
        var first = await new TemporalDevServer(ChildEnvironment.Start).StartAsync(start, ct);   // the serve that started it is gone; its server isn't
        var next = new TemporalDevServer(ChildEnvironment.Start);
        try
        {
            var adopted = await next.StartAsync(start, ct);

            Assert.Equal((first.ProcessId, first.Frontend.Port, true), (adopted.ProcessId, adopted.Frontend.Port, adopted.Adopted));
        }
        finally
        {
            await next.StopAsync(first, ct);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task A_server_on_the_home_database_that_doesnt_answer_is_replaced()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new TemporalDevServer(ChildEnvironment.Start);
        var start = new TemporalServerStart(PinnedTemporal.Path(), _home.Path);
        var stuck = await server.StartAsync(start, ct);
        DevServerProcesses.Signal(ChildEnvironment.Start, stuck.ProcessId, "STOP");   // alive, holding the database, answering nothing
        RunningTemporalServer? replacement = null;
        try
        {
            replacement = await server.StartAsync(start, ct);

            Assert.NotEqual(stuck.ProcessId, replacement.ProcessId);
            Assert.False(replacement.Adopted);
            Assert.True(DevServerProcesses.Gone(stuck.ProcessId), "the stuck server still runs");
            Assert.Equal(DevServerAnswer.Ready, (await DevServerProbe.Ask(replacement.Frontend, TemporalSettings.DefaultNamespace, TimeSpan.FromSeconds(5), ct)).Answer);
        }
        finally
        {
            DevServerProcesses.Signal(ChildEnvironment.Start, stuck.ProcessId, "KILL");
            if (replacement is not null)
            {
                await server.StopAsync(replacement, ct);
            }
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task A_restart_on_the_same_database_keeps_the_namespace_and_the_search_attributes()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new TemporalDevServer(ChildEnvironment.Start);
        var start = new TemporalServerStart(PinnedTemporal.Path(), _home.Path);
        await server.StopAsync(await server.StartAsync(start, ct), ct);

        var restarted = await server.StartAsync(start, ct);
        try
        {
            Assert.False(restarted.Adopted);
            Assert.Equal(DevServerAnswer.Ready, (await DevServerProbe.Ask(restarted.Frontend, TemporalSettings.DefaultNamespace, TimeSpan.FromSeconds(5), ct)).Answer);
            var listed = await Temporalio.Client.TemporalConnection.CreateLazy(new(restarted.Frontend.ToString())).OperatorService
                .ListSearchAttributesAsync(new() { Namespace = TemporalSettings.DefaultNamespace }, new() { CancellationToken = ct });
            Assert.All(SindriTemporal.SearchAttributes, a => Assert.Equal(a.Type, listed.CustomAttributes[a.Name]));
        }
        finally
        {
            await server.StopAsync(restarted, ct);
        }
    }
}
