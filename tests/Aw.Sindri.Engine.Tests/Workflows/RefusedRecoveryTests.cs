using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing.Judge;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Issue 1468: a run whose report was refused twice (the second ends its session, so the watch reads 'refused') is not completed from the output file the agent left on disk, the very
/// output that was refused. It ends as refused, and the model pick's one retry on a stronger model follows.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RefusedRecoveryTests : AgentRuntimeTestBase
{
    private static readonly ModelFacts[] _three =
    [
        new("haiku", "Haiku", ModelSource.Catalog, 0),
        new("sonnet", "Sonnet", ModelSource.Catalog, 1),
        new("opus", "Opus", ModelSource.Catalog, 2),
    ];

    // A recipe whose fact:resolution is a payload, so the report check can refuse it as the engine does for a real agent.
    private static readonly string _payloadRecipe = string.Join('\n',
        "schema: 1",
        "id: workflow.dev-agent-payload",
        "version: 1",
        "description: \"A Step an agent session reports with a typed payload (issue 1468 tests).\"",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "  - { key: fact:issue, label: \"Issue\", type: text, kind: text }",
        "outputs:",
        "  - { key: fact:resolution, label: \"Resolution\", type: payload, from: write }",
        "initial: write",
        "states:",
        "  write:",
        "    kind: active",
        "    step: step.report.write",
        "    budget: { tokens: 1000 }",
        "    timeout: { after: 1m, then: park }",
        "    next: \"Write up the resolution for {item}.\"",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: write, to: done, on: step.completed, when: { outcome: done }, produces: [fact:resolution] }");

    /// <summary>The brief's path: the agent reports a non-JSON payload twice through the report check (the first answer is "one retry", the second records the refusal and stops the session), the session ends as a stopped one does, and its output stays on disk.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_run_refused_twice_with_its_output_left_on_disk_is_retried_not_recovered()
    {
        var ct = TestContext.Current.CancellationToken;
        UsePick();
        _root = "workflow.dev-agent-payload";
        _also = [_payloadRecipe];
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var first = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _diff.Add(rig.Worktree + "-e1");
            _outputs.Add(rig.Worktree, "resolution", "the output that was refused");
            var view = await handle.QueryAsync(static wf => wf.GetInstance());
            var bad = new CompleteStepRequest(view.Address.Value, first.Run!.Value, OutcomeName.From("done"), new Dictionary<FactKey, ReadOnlyMemory<byte>> { [FactKey.From("fact:resolution")] = System.Text.Encoding.UTF8.GetBytes("not json") });
            var checks = rig.Sindri.GetRequiredService<ReportOutputTypes>();

            var one = await checks.Check(view, bad, ct);
            var two = await checks.Check(view, bad, ct);
            _sessions.End(rig.Launch, SessionEndReason.Exit, exitCode: 143);   // the launcher stopped the session; 'session wrap' reports its exit

            Assert.Contains("one retry", one?.Message, StringComparison.Ordinal);
            Assert.Contains("second bad report", two?.Message, StringComparison.Ordinal);
            Assert.Equal([rig.Launch], _launcher.Stops);   // the second refusal stopped it
            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            var running = await WorkflowsHarness.Until(handle, v => v.AgentRun?.Launch == rig.EscalatedLaunch.Value);

            Assert.Equal([rig.Launch, rig.EscalatedLaunch], _launcher.Requests.Select(static r => r.Launch));
            Assert.Equal("write", running.State);   // not 'done': the refused output was not accepted from disk
            var picks = await Picked(rig, ct);
            var retried = picks[^1];
            Assert.Equal(("refused", rig.EscalatedLaunch.Value), (retried.GetProperty("escalatedFromKind").GetString(), retried.GetProperty("launchId").GetString()));   // the first run ended as refused: that kind reached the retry
        });
    }

    private static async Task<List<JsonElement>> Picked(AgentRig rig, CancellationToken ct)
    {
        var rows = (await rig.Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 500, ct)).Value;
        var found = new List<JsonElement>();
        foreach (var row in rows.Where(static e => e.Envelope.Type == "model.picked"))
        {
            using var document = JsonDocument.Parse(row.Envelope.Payload);
            found.Add(document.RootElement.Clone());
        }

        return found;
    }

    [Fact(Timeout = 120_000)]
    public async Task A_session_that_ended_without_a_refusal_is_still_recovered_from_disk()
    {
        var ct = TestContext.Current.CancellationToken;
        UsePick();
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _outputs.Add(rig.Worktree, "resolution", "The button handler was missing.");
            _sessions.End(rig.Launch, SessionEndReason.Exit, exitCode: 0);

            var done = await WorkflowsHarness.Until(handle, static v => v.State == "done");

            Assert.Equal("done", done.State);
            _ = Assert.Single(_launcher.Requests);
        });
    }

    private void UsePick()
    {
        var judge = new FakeModelPickJudge().Chooses("m1");
        _extra = services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickJudge>(judge));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickSettings>(new FakeModelPickSettings { Settings = new ModelPickSettings(PickOn: true) }));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickHistory>(new FakeModelPickHistory()));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelCatalog>(new FakeModelCatalog().With(AgentId.From("claude-code"), _three)));
        };
    }
}
