using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Judge;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The escalation retry of the model pick (model-pick spec 5, 7.1; owner decision M3, 2026-10-27): a refused cheap run is launched once more on a stronger model, under its own '-e1' launch id,
/// in the same run, with no round counted and no special token cap. Run through the workflow with FakeLauncher, FakeAgentSessions and the fake judge.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ModelEscalationTests : AgentRuntimeTestBase
{
    private static readonly ModelFacts[] _three =
    [
        new("haiku", "Haiku", ModelSource.Catalog, 0),
        new("sonnet", "Sonnet", ModelSource.Catalog, 1),
        new("opus", "Opus", ModelSource.Catalog, 2),
    ];

    [Fact(Timeout = 120_000)]
    public async Task A_refused_cheap_run_is_retried_once_on_a_stronger_model_under_its_own_launch_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var judge = UsePick(new ModelPickSettings(PickOn: true));
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var first = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            var retry = rig.EscalatedLaunch;
            _diff.Add(rig.Worktree + "-e1");

            Refuse(rig, rig.Launch, tokens: 300);
            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            var running = await WorkflowsHarness.Until(handle, v => v.AgentRun?.Launch == retry.Value);

            Assert.Equal([rig.Launch, retry], _launcher.Requests.Select(static r => r.Launch));
            Assert.Equal(["haiku", "sonnet"], _launcher.Requests.Select(static r => r.Step?.Model), StringComparer.Ordinal);   // the retry picks above the model that failed
            Assert.Equal(rig.Worktree + "-e1", _launcher.Requests[1].Worktree, StringComparer.Ordinal);
            Assert.Equal(["sonnet", "opus"], judge.Asks[1].Candidates.Select(static c => c.Model), StringComparer.Ordinal);   // nothing at or below haiku is offered
            Assert.Equal((first.Run, 1), (running.Run, running.AgentRun?.Escalation));   // the same run: no new attempt, no round

            _sessions.Spend(retry, 1, 500);
            var result = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(first.Run!.Value)), new WorkflowUpdateOptions(new string('a', 32)));
            await WaitFor(() => _launcher.Stops.Contains(retry), ct);   // the close names the -e1 id too

            Assert.Equal("done", result.Instance.State);
            Assert.Contains(rig.Launch, _launcher.Stops);
            Assert.Equal(2, (await Rows(rig, "launch.started", ct)).Count);
            var picks = await Rows(rig, "model.picked", ct);
            Assert.Equal(2, picks.Count);
            Assert.Equal((rig.Launch.Value, "haiku", null, null), (Text(picks[0], "launchId"), Text(picks[0], "model"), Text(picks[0], "escalatedFrom"), Text(picks[0], "escalatedFromKind")));
            Assert.Equal((retry.Value, "sonnet", "haiku", "refused"), (Text(picks[1], "launchId"), Text(picks[1], "model"), Text(picks[1], "escalatedFrom"), Text(picks[1], "escalatedFromKind")));
        });
    }

    /// <summary>M3: the retry's tokens are counted by the normal budgets, with no cap of their own. The failed run's 300 and the retry's 500 are what the state is charged.</summary>
    [Fact(Timeout = 120_000)]
    public async Task The_retrys_tokens_and_the_failed_runs_are_charged_to_the_normal_budget()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = UsePick(new ModelPickSettings(PickOn: true));
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var first = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _diff.Add(rig.Worktree + "-e1");
            Refuse(rig, rig.Launch, tokens: 300);
            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            _sessions.Spend(rig.EscalatedLaunch, 1, 500);   // more than twice the failed run's spend: no 2 x cap stops it

            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(first.Run!.Value)), new WorkflowUpdateOptions(new string('b', 32)));

            Assert.Equal(800, await Tokens(rig, "step.completed", ct));
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_retry_that_fails_too_fails_the_step_and_a_third_model_is_not_tried()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = UsePick(new ModelPickSettings(PickOn: true));
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _diff.Add(rig.Worktree + "-e1");
            Refuse(rig, rig.Launch, tokens: 300);
            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            Refuse(rig, rig.EscalatedLaunch, tokens: 400);

            Assert.Equal("exit", await rig.FailureReason(ct));
            await WaitFor(() => _launcher.Requests.Count == 3, ct);   // the engine's own retry of the Step: a fresh run, picked again from the cheapest
            Assert.Equal([rig.Launch, rig.EscalatedLaunch], _launcher.Requests.Take(2).Select(static r => r.Launch));
            Assert.Equal("haiku", _launcher.Requests[2].Step?.Model);
        });
    }

    /// <summary>M3 on the failure path: the state's budget is 1,000 tokens. The refused first launch spends 600 and the failed retry 600, so the 1,200 are charged and the Step's next start parks for budget (600 alone would have started it).</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_failed_retry_charges_both_launches_and_the_next_start_parks_for_budget()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = UsePick(new ModelPickSettings(PickOn: true));
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _diff.Add(rig.Worktree + "-e1");
            Refuse(rig, rig.Launch, tokens: 600);
            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            Refuse(rig, rig.EscalatedLaunch, tokens: 600);

            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal(ParkReason.Budget, parked.Question?.Reason);
            Assert.Equal(2, _launcher.Requests.Count);   // no third launch: the engine's own retry of the Step did not start
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_run_on_the_strongest_allowed_model_is_not_retried()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = UsePick(new ModelPickSettings(PickOn: true, Pin: "opus"));
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            Refuse(rig, rig.Launch, tokens: 300);

            Assert.Equal("exit", await rig.FailureReason(ct));
            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            Assert.NotEqual(rig.EscalatedLaunch, _launcher.Requests[1].Launch);   // the second launch is the engine's own retry, not an escalation
            Assert.Empty(await Rows(rig, "model.picked", ct, escalated: true));
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_crashed_session_is_not_escalated()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = UsePick(new ModelPickSettings(PickOn: true));
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _sessions.End(rig.Launch, SessionEndReason.Lost);

            Assert.Equal("lost", await rig.FailureReason(ct));
            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            Assert.NotEqual(rig.EscalatedLaunch, _launcher.Requests[1].Launch);
        });
    }

    private FakeModelPickJudge UsePick(ModelPickSettings settings)
    {
        var judge = new FakeModelPickJudge().Chooses("m1");   // the cheapest candidate, whatever is left
        _extra = services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickJudge>(judge));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickSettings>(new FakeModelPickSettings { Settings = settings }));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickHistory>(new FakeModelPickHistory()));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelCatalog>(new FakeModelCatalog().With(AgentId.From("claude-code"), _three)));
        };
        return judge;
    }

    // The session ends for a refused report: the engine reads 'refused' and the run's spend.
    private void Refuse(AgentRig rig, LaunchId launch, long tokens)
    {
        rig.Sindri.GetRequiredService<ReportRefusals>().Record(launch, "fact:resolution is not valid JSON");
        _sessions.Spend(launch, 1, tokens);
        _sessions.End(launch, SessionEndReason.Exit, exitCode: 0);
    }

    private static async Task<List<JsonElement>> Rows(AgentRig rig, string type, CancellationToken ct, bool escalated = false)
    {
        var rows = (await rig.Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 500, ct)).Value;
        var found = new List<JsonElement>();
        foreach (var row in rows.Where(e => e.Envelope.Type == type))
        {
            using var document = JsonDocument.Parse(row.Envelope.Payload);
            if (!escalated || Text(document.RootElement, "escalatedFrom") is not null)
            {
                found.Add(document.RootElement.Clone());
            }
        }

        return found;
    }

    // The tokens of the instance's one transition on the trigger, waiting for the row (the mirror writes it after the state moves).
    private static async Task<int> Tokens(AgentRig rig, string trigger, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime.AddSeconds(30);
        while (RealTime.NowDateTime < deadline)
        {
            foreach (var row in await Rows(rig, "workflow.transition", ct))
            {
                if (Text(row, "trigger") == trigger && row.GetProperty("detail").TryGetProperty("tokens", out var tokens))
                {
                    return tokens.GetInt32();
                }
            }

            await Task.Delay(50, ct);
        }

        return -1;
    }

    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
