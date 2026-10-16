using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The read verbs on the pinned dev server (engine spec §6.3, §6.4; engine P4 unit (e)): 'status' shows the desk-task
/// layout from the 'GetInstance' Query, 'list' finds the instance by its search attributes ('--needs-me' under Tasks,
/// '--item', '--state'), and 'events' reads the instance's rows from the ledger. The instance is started through the
/// client here, because 'workflow start' is unit (b)'s.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowReadVerbsTests : IDisposable
{
    private const string Address = "T-1/step/1";
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Status_list_and_events_show_a_started_instance_and_its_desk_task()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);   // the read verbs are clients of serve's routes
        var sindri = served.Sindri;
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-1", ct));
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

            var (statusExit, status, statusErr) = await WorkflowsHarness.Cli(sindri, "status", Address);
            var (jsonExit, json, _) = await WorkflowsHarness.Cli(sindri, "status", Address, "--json");
            var needsMe = await WorkflowsHarness.Listed(sindri, ct, "list", "--needs-me");
            var byItem = await WorkflowsHarness.Listed(sindri, ct, "list", "--item", "T-1");
            var (_, otherState, _) = await WorkflowsHarness.Cli(sindri, "list", "--state", "checks");
            var (eventsExit, events) = await EventsOnceNoticed(sindri);
            Assert.Equal((0, string.Empty), (statusExit, statusErr));
            AssertDeskTaskLayout(status);
            Assert.Equal(0, jsonExit);
            using var document = JsonDocument.Parse(json);
            Assert.Equal(("CompleteStep", 1), (document.RootElement.GetProperty("allowedCommands")[0].GetProperty("actionId").GetString(), document.RootElement.GetProperty("version").GetInt32()));

            Assert.Matches(@"^Tasks\n  T-1/step/1  workflow\.step@[0-9a-f]{12}  v1  produce: desk task  since .+\n$", needsMe);
            Assert.Matches(@"^T-1/step/1  workflow\.step@", byItem);
            Assert.Equal("no instances match\n", otherState);
            Assert.Equal(0, eventsExit);
            Assert.Matches(@"^\d+  \d\d-\d\d \d\d:\d\d:\d\d  workflow\.started  -> produce  start  \S+\n\d+  \d\d-\d\d \d\d:\d\d:\d\d  outbox\.pending  \S+\n\d+  \d\d-\d\d \d\d:\d\d:\d\d  outbox\.dispatched  \S+\n$", events);   // the first desk task's engine notice: both its rows (P6 units (c), (d))
            await handle.TerminateAsync("test over");
        }
    }

    /// <summary>'workflow events' once the first desk task's engine notice is dispatched: it skips the delay, so it lands a moment after the instance shows the task.</summary>
    private static async Task<(int Exit, string Events)> EventsOnceNoticed(IServiceProvider sindri)
    {
        var (exit, events, _) = await WorkflowsHarness.Cli(sindri, "events", Address);
        for (var tries = 0; tries < 100 && !events.Contains("outbox.dispatched", StringComparison.Ordinal); tries++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            (exit, events, _) = await WorkflowsHarness.Cli(sindri, "events", Address);
        }

        return (exit, events);
    }

    /// <summary>The §6.4 desk-task layout for T-1/step/1, run 1.1.</summary>
    private static void AssertDeskTaskLayout(string status)
    {
        var lines = status.TrimEnd('\n').Split('\n');
        Assert.Matches(@"^T-1/step/1  workflow\.step@[0-9a-f]{12}  v1  produce: desk task  since \d\d:\d\d \(.+\)$", lines[0]);
        Assert.Equal("  next      Produce the Step output for T-1 (round 1/3), then run:", lines[1]);
        Assert.Equal("            sindri workflow complete T-1/step/1 --run 1.1 --outcome done --output fact:output=<file>", lines[2]);
        Assert.Equal("  inputs    fact:spec  spec.md  sha256:aaaa...", lines[3]);
        Assert.Matches(@"^  timeout   parks in .+ \(desk\)$", lines[4]);
        Assert.Equal("  budget    tokens 0 / 200k (state), 0 / 400k (instance)", lines[5]);
        Assert.Equal("  commands  complete, takeover, cancel", lines[6]);
    }

    [Fact(Timeout = 120_000)]
    public async Task Status_of_an_unknown_instance_is_SND_WF_001_and_exit_2()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);

        var (exit, stdout, stderr) = await WorkflowsHarness.Cli(served.Sindri, "status", "T-9/step/1");

        Assert.Equal((2, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-WF-001 T-9/step/1 isn't an instance", stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("list", "--needs-me", "--item", "T-1")]
    [InlineData("list", "--colour", "blue")]
    [InlineData("status")]
    [InlineData("status", "--json")]
    [InlineData("events")]
    public async Task Malformed_read_verbs_are_SND_CLI_003_and_exit_2(params string[] args)
    {
        await using var sindri = _harness.Sindri();

        var (exit, stdout, stderr) = await WorkflowsHarness.Cli(sindri, args);

        Assert.Equal((2, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-CLI-003", stderr, StringComparison.Ordinal);
    }
}
