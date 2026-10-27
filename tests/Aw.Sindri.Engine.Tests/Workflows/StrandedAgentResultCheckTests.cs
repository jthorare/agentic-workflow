using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The #1000 check: an agent Step whose session ended without a report (its 'workflow complete' got no answer while the control plane restarted) must not be left running or failed
/// blind. The result sits in the run directory's '.sindri-out': the run completes with it, and when what is there proves nothing the run parks saying where. The run directory is a real
/// temp folder (the instance holds it as 'fact:workspace', so the launch runs there); only the launcher and the session watch are fakes. Behaviour only, so the same file holds before and after the fix.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class StrandedAgentResultCheckTests : AgentRuntimeTestBase
{
    private const int RunsBeforeThePark = 3;   // the first run and the engine's two retries
    private readonly string _runDirectory = Path.Combine(Path.GetTempPath(), "aw-stranded-" + Path.GetRandomFileName());

    public StrandedAgentResultCheckTests()
    {
        _ = Directory.CreateDirectory(Path.Combine(_runDirectory, ".sindri-out"));
        _root = "workflow.stranded-agent";
        _also = [_strandedRecipe];
        _more = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:workspace"] = _runDirectory };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_runDirectory))
        {
            Directory.Delete(_runDirectory, recursive: true);
        }

        base.Dispose(disposing);
    }

    [Theory(Timeout = 120_000)]
    [InlineData(SessionEndReason.Idle)]
    [InlineData(SessionEndReason.Lost)]
    [InlineData(SessionEndReason.Exit)]
    public async Task A_session_that_ended_with_its_result_on_disk_completes_the_run_from_it(SessionEndReason ended)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await WaitFor(() => _launcher.Requests.Count > 0, ct);
            await File.WriteAllTextAsync(Path.Combine(_runDirectory, ".sindri-out", "resolution"), "The button handler was missing.", ct);   // written after the launch, as the agent does
            _sessions.End(_launcher.Requests[0].Launch, ended, exitCode: ended == SessionEndReason.Exit ? 0 : null);

            var done = await WorkflowsHarness.Until(handle, static v => v.State == "done");

            Assert.Null(done.Question);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_session_that_ended_with_files_that_prove_nothing_parks_naming_the_output_folder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await File.WriteAllTextAsync(Path.Combine(_runDirectory, ".sindri-out", "scratch.txt"), "half a thought", ct);
            for (var run = 0; run < RunsBeforeThePark; run++)   // every run, a retry included, ends the same way
            {
                var seen = run;
                await WaitFor(() => _launcher.Requests.Count > seen, ct);
                _sessions.End(_launcher.Requests[run].Launch, SessionEndReason.Idle);
            }

            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Contains("the agent finished but its result was not recorded: " + Path.Combine(_runDirectory, ".sindri-out"), parked.Question?.Question, StringComparison.Ordinal);
        });
    }

    private static readonly string _strandedRecipe = string.Join('\n',
        "schema: 1",
        "id: workflow.stranded-agent",
        "version: 1",
        "description: \"A Step an agent session runs, in a workspace the test owns (#1000 check).\"",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "  - { key: fact:issue, label: \"Issue\", type: text, kind: text }",
        "  - { key: fact:workspace, label: \"Workspace\", type: text, kind: text }",
        "initial: write",
        "states:",
        "  write:",
        "    kind: active",
        "    step: step.report.write",
        "    budget: { tokens: 1000 }",
        "    timeout: { after: 10m, then: park }",
        "    next: \"Write up the resolution for {item}.\"",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: write, to: done, on: step.completed, when: { outcome: done }, produces: [fact:resolution] }");
}
