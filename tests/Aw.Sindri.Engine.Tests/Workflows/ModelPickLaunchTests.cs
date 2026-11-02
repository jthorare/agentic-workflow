using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Judge;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The model pick in the launch activity (model-pick spec 4, 5, 6): the candidates, Jev's answer or the rules fallback, and the pick stored before the session starts so a retry never asks twice.</summary>
[Lane(TestLane.Integration)]
public sealed class ModelPickLaunchTests : IDisposable
{
    private const string Repo = "app";
    private const string Item = "ITM-00000000000000000000000000000009";
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly ModelFacts[] _three =
    [
        new("haiku", "Haiku", ModelSource.Catalog, 0),
        new("sonnet", "Sonnet", ModelSource.Catalog, 1),
        new("opus", "Opus", ModelSource.Catalog, 2),
    ];

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Pick_off_runs_the_agents_default_and_asks_no_judge()
    {
        await using var rig = await Rig(new ModelPickSettings(PickOn: false), TestContext.Current.CancellationToken);

        _ = await rig.Launch(Run(1), floor: null, TestContext.Current.CancellationToken);

        Assert.Null(rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
        Expect.Equal("default/", await rig.Picked("by", "model", TestContext.Current.CancellationToken));
        Expect.Equal("default/pick-off", await rig.Picked("by", "reason", TestContext.Current.CancellationToken));
    }

    /// <summary>Model-pick M-9: with the pick off a step's floor still holds, so an install that never turned the pick on does not draft below strong.</summary>
    [Theory(Timeout = 120_000)]
    [InlineData("strong", "opus")]
    [InlineData("standard", "sonnet")]
    [InlineData("cheap", "haiku")]
    public async Task Pick_off_with_a_floor_runs_the_cheapest_model_at_or_above_it(string floor, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: false), ct);

        _ = await rig.Launch(Run(1), floor, ct);

        Expect.Equal(expected, rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
        Expect.Equal($"constraint/{expected}", await rig.Picked("by", "model", ct));
        Expect.Equal("constraint/floor", await rig.Picked("by", "reason", ct));
    }

    /// <summary>The scoping recipe's challenge state: floor standard, and the drafter on opus leaves one model.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_challenger_with_a_standard_floor_and_the_drafter_on_opus_runs_on_sonnet()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct, drafter: new DrafterRan("opus"));

        _ = await rig.Launch(Challenge(1), floor: "standard", ct, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("sonnet", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
    }

    [Fact(Timeout = 120_000)]
    public async Task One_candidate_is_the_pick_and_asks_no_judge()
    {
        await using var rig = await Rig(new ModelPickSettings(PickOn: true, Pin: "sonnet"), TestContext.Current.CancellationToken);

        _ = await rig.Launch(Run(1), floor: null, TestContext.Current.CancellationToken);

        Expect.Equal("sonnet", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
        Expect.Equal("constraint/sonnet", await rig.Picked("by", "model", TestContext.Current.CancellationToken));
        Expect.Equal("constraint/pin", await rig.Picked("by", "reason", TestContext.Current.CancellationToken));
    }

    /// <summary>A Step that names its model keeps it (issue 966): the recipe author's explicit choice, so no judge call.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_step_that_names_its_model_keeps_it_and_asks_no_judge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);

        _ = await rig.Launch(Run(1), floor: null, ct, model: "opus");

        Expect.Equal("opus", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
        Expect.Equal("constraint/opus", await rig.Picked("by", "model", ct));
        Expect.Equal("constraint/step", await rig.Picked("by", "reason", ct));
    }

    [Fact(Timeout = 120_000)]
    public async Task An_agent_that_offers_one_model_picks_it_with_the_reason_only_model()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true, Models: [new ModelFacts("sonnet", "Sonnet", ModelSource.Profile, 0)]), ct);

        _ = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal(0, rig.Judge.Asks.Count);
        Expect.Equal("constraint/sonnet", await rig.Picked("by", "model", ct));
        Expect.Equal("constraint/only-model", await rig.Picked("by", "reason", ct));
    }

    [Fact(Timeout = 120_000)]
    public async Task The_launched_run_carries_the_run_directory_the_launcher_named_and_none_when_it_named_none()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: false), ct);
        rig.Launcher.RunDirectory = "/home/sindri/workflow/runs/WFI-1/draft/LCH-1";

        var named = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal("/home/sindri/workflow/runs/WFI-1/draft/LCH-1", named.RunDirectory);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_launch_with_no_run_directory_leaves_it_null()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: false), ct);

        Assert.Null((await rig.Launch(Run(1), floor: null, ct)).RunDirectory);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_strong_floor_leaves_one_candidate()
    {
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), TestContext.Current.CancellationToken);

        _ = await rig.Launch(Run(1), floor: "strong", TestContext.Current.CancellationToken);

        Expect.Equal("opus", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
    }

    [Fact(Timeout = 120_000)]
    public async Task Jevs_answer_is_the_model_and_picking_comes_before_picked()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Chooses("m2");

        _ = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal("sonnet", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal("m1=haiku,m2=sonnet,m3=opus", string.Join(',', rig.Judge.Asks[0].Candidates.Select(c => c.Key + "=" + c.Model)));
        Expect.Equal("jev/sonnet", await rig.Picked("by", "model", ct));
        var launch = AgentLaunches.LaunchIdFor(Run(1)).Value;
        Expect.Equal($"model.picking:{launch},model.pick:{launch}", string.Join(',', (await rig.Rows(ct)).Where(e => e.Envelope.Type.StartsWith("model.pick", StringComparison.Ordinal)).Select(e => e.IdempotencyKey)));
    }

    [Fact(Timeout = 120_000)]
    public async Task Launch_started_carries_the_pick()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Chooses("m1");

        _ = await rig.Launch(Run(1), floor: null, ct);

        var started = (await rig.Rows(ct)).First(e => e.Envelope.Type == "launch.started");
        using var document = JsonDocument.Parse(started.Envelope.Payload);
        Expect.Equal("haiku", document.RootElement.GetProperty("model").GetString());
    }

    [Fact(Timeout = 120_000)]
    public async Task A_retry_reads_the_stored_pick_and_asks_the_judge_once()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Chooses("m2");

        _ = await rig.Launch(Run(1), floor: null, ct);
        _ = rig.Judge.Chooses("m3");   // a second ask would pick opus
        _ = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal(1, rig.Judge.Asks.Count);
        Expect.Equal("sonnet,sonnet", string.Join(',', rig.Launcher.Requests.Select(r => r.Step?.Model)));
    }

    [Fact(Timeout = 120_000)]
    public async Task A_cheap_pick_with_a_stronger_model_allowed_can_escalate_and_reports_its_model()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Chooses("m1");

        var launched = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal("haiku/True", launched.Model + "/" + launched.CanEscalate);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("m3", null, null)]       // opus is the strongest model there is
    [InlineData("m2", "sonnet", null)]   // the cap leaves nothing above sonnet
    [InlineData(null, null, "sonnet")]   // a pin leaves one model
    public async Task A_pick_with_nothing_stronger_allowed_cannot_escalate(string? key, string? cap, string? pin)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true, Cap: cap, Pin: pin), ct);
        _ = rig.Judge.Chooses(key ?? "m1");

        var launched = await rig.Launch(Run(1), floor: null, ct);

        Assert.False(launched.CanEscalate);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_step_that_names_its_model_cannot_escalate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);

        var launched = await rig.Launch(Run(1), floor: null, ct, model: "haiku");

        Assert.False(launched.CanEscalate);
    }

    /// <summary>A Step that reads a workspace would retry in the failed run's dirty worktree (spec 5), so it does not escalate.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_step_that_reads_a_workspace_cannot_escalate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Chooses("m1");

        var launched = await rig.Launch(Run(1), floor: null, ct, workspace: _harness.Fact("/work/phase"));

        Expect.Equal("haiku/False", launched.Model + "/" + launched.CanEscalate);
    }

    [Fact(Timeout = 120_000)]
    public async Task An_escalation_picks_above_the_failed_model_records_why_and_cannot_escalate_again()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Chooses("m1");
        var retry = Run(1) with { Escalation = 1 };

        var launched = await rig.Launch(retry, floor: null, ct, previous: new PreviousRun("haiku", "refused", Check: null, Tokens: 300), escalation: 1);

        Expect.Equal("sonnet,opus", string.Join(',', rig.Judge.Asks[0].Candidates.Select(c => c.Model)));
        Expect.Equal("sonnet/False", launched.Model + "/" + launched.CanEscalate);   // opus is still allowed, but one retry is all there is
        Expect.Equal("haiku/refused", await rig.Picked("escalatedFrom", "escalatedFromKind", ct));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("below-threshold", "below-threshold")]
    [InlineData("unavailable", "judge-unavailable")]
    [InlineData("rules-no-key", "no-api-key")]
    [InlineData("unknown-key", "unknown-key")]
    public async Task Each_rules_reason_runs_the_cheapest_allowed_model(string scenario, string reason)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Answers(Answer(scenario));

        _ = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal("haiku", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal($"rules/{reason}", await rig.Picked("by", "reason", ct));
    }

    /// <summary>The second opinion on the drafter's only agent (#1008): the drafter's model is out of the candidates, so the challenger runs on another one.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_challenger_on_the_drafters_only_agent_is_picked_among_the_other_models()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct, drafter: new DrafterRan("opus"));
        _ = rig.Judge.Chooses("m1");

        var launched = await rig.Launch(Challenge(1), floor: null, ct, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("no failure", launched.Failure is { } failure ? failure.Code + ": " + failure.Message : "no failure");
        Expect.Equal("haiku,sonnet", string.Join(',', rig.Judge.Asks[0].Candidates.Select(c => c.Model)));
        Expect.Equal("haiku", rig.Launcher.Requests[0].Step?.Model);
    }

    /// <summary>The retry of a challenger keeps the drafter's exclusion (#1008, spec 4.1): above the failed model and not the drafter's model, so with the drafter on opus there is nothing above sonnet.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_challenger_cannot_escalate_to_the_drafters_model()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct, drafter: new DrafterRan("opus"));
        _ = rig.Judge.Chooses("m2");   // haiku, sonnet are left: sonnet

        var launched = await rig.Launch(Challenge(1), floor: null, ct, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("sonnet/False", launched.Model + "/" + launched.CanEscalate);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_challengers_escalation_picks_above_the_failed_model_without_the_drafters()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct, drafter: new DrafterRan("sonnet"));
        _ = rig.Judge.Chooses("m1");
        var first = await rig.Launch(Challenge(1), floor: null, ct, agent: new StateAgent(DifferentFrom: "draft"));   // haiku, opus are left: haiku, and opus is above it
        var retry = Challenge(1) with { Escalation = 1 };

        var launched = await rig.Launch(retry, floor: null, ct, previous: new PreviousRun("haiku", "refused", Check: null, Tokens: 300), escalation: 1, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("haiku/True", first.Model + "/" + first.CanEscalate);
        Expect.Equal("opus/False", launched.Model + "/" + launched.CanEscalate);   // sonnet is the drafter's: opus is the only model left
    }

    [Fact(Timeout = 120_000)]
    public async Task A_challenger_whose_only_model_is_the_drafters_parks_with_SND_MODEL_003()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true, Models: [new ModelFacts("sonnet", "Sonnet", ModelSource.Profile, 0)]), ct, drafter: new DrafterRan("sonnet"));

        var launched = await rig.Launch(Challenge(1), floor: null, ct, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("SND-MODEL-003", launched.Failure?.Code ?? "no failure");
        Expect.Equal(0, rig.Launcher.Requests.Count);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_challenger_on_the_drafters_only_agent_parks_when_the_drafters_model_was_not_recorded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct, drafter: new DrafterRan(Model: null));

        var launched = await rig.Launch(Challenge(1), floor: null, ct, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("SND-MODEL-003", launched.Failure?.Code ?? "no failure");
    }

    [Fact(Timeout = 120_000)]
    public async Task A_challenger_moves_to_a_second_usable_agent_when_the_catalog_has_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct, drafter: new DrafterRan("opus"), agents: [AgentEntries.ClaudeCode, AgentEntries.Codex]);

        var launched = await rig.Launch(Challenge(1), floor: null, ct, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("no failure", launched.Failure is { } failure ? failure.Code + ": " + failure.Message : "no failure");
        Expect.Equal("codex", rig.Launcher.Requests[0].Agent.Value);
    }

    /// <summary>A state's 'agent: codex' (#1269) runs its Step on that agent, not the profile's.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_state_that_names_an_agent_runs_on_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: false), ct, agents: [AgentEntries.ClaudeCode, AgentEntries.Codex]);

        var launched = await rig.Launch(Run(1), floor: null, ct, agent: new StateAgent(Use: "codex"));

        Expect.Equal("no failure", launched.Failure is { } failure ? failure.Code + ": " + failure.Message : "no failure");
        Expect.Equal("codex", rig.Launcher.Requests[0].Agent.Value);
    }

    /// <summary>'agent: { differentFrom: draft }' is the general rule the scoping challenger used to be hard-coded for (#1269): any Step can declare it.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_state_that_differs_from_another_moves_to_a_second_usable_agent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: false), ct, drafter: new DrafterRan("opus"), agents: [AgentEntries.ClaudeCode, AgentEntries.Codex]);

        var launched = await rig.Launch(Run(1), floor: null, ct, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("no failure", launched.Failure is { } failure ? failure.Code + ": " + failure.Message : "no failure");
        Expect.Equal("codex", rig.Launcher.Requests[0].Agent.Value);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_state_that_differs_from_another_on_the_only_agent_picks_another_model()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct, drafter: new DrafterRan("opus"));
        _ = rig.Judge.Chooses("m1");

        _ = await rig.Launch(Run(1), floor: null, ct, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("haiku,sonnet", string.Join(',', rig.Judge.Asks[0].Candidates.Select(c => c.Model)));
    }

    /// <summary>With no 'agent:' key a Step that is not the scoping challenger differs from nothing, so the drafter's model stays a candidate.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_state_with_no_agent_key_differs_from_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct, drafter: new DrafterRan("opus"));
        _ = rig.Judge.Chooses("m1");

        _ = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal("haiku,sonnet,opus", string.Join(',', rig.Judge.Asks[0].Candidates.Select(c => c.Model)));
    }

    /// <summary>An escalation retry of a 'differentFrom' state keeps the agent its first run was placed on (#1269), and its launch carries that agent and the retry's model.</summary>
    [Fact(Timeout = 120_000)]
    public async Task An_escalation_retry_of_a_differentFrom_state_stays_on_the_agent_of_its_first_run()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct, drafter: new DrafterRan("opus", AgentId.From("codex")), agents: [AgentEntries.ClaudeCode, AgentEntries.Codex]);
        _ = rig.Judge.Chooses("m1");
        var retry = Challenge(1) with { Escalation = 1 };

        var launched = await rig.Launch(retry, floor: null, ct, previous: new PreviousRun("haiku", "refused", Check: null, Tokens: 300), escalation: 1, agent: new StateAgent(DifferentFrom: "draft"));

        Expect.Equal("no failure", launched.Failure is { } failure ? failure.Code + ": " + failure.Message : "no failure");
        Expect.Equal("codex", rig.Launcher.Requests[0].Agent.Value);
        Expect.Equal("codex/sonnet", launched.Agent + "/" + launched.Model);
    }

    private static AgentStepRun Challenge(int attempt)
    {
        var address = InstanceAddress.From("T-9/step/" + attempt.ToString(CultureInfo.InvariantCulture));
        return new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From(attempt.ToString(CultureInfo.InvariantCulture) + ".1"), new StepRef("challenge", "step.scope.challenge"), WriteScope: false);
    }

    private static ChooseAnswer Answer(string scenario) => scenario switch
    {
        "below-threshold" => new ChooseAnswer(ChooseStatus.Escalated, Choice: null, "below-threshold", DecisionRef: "decision-2"),
        "unavailable" => new ChooseAnswer(ChooseStatus.Failed, Choice: null, "judge model-pick did not finish (judge: TimedOut)"),
        "rules-no-key" => new ChooseAnswer(ChooseStatus.Decided, "m1", "no-api-key", Confidence: 1.0, Provider: "rules", DecisionRef: "decision-3"),
        _ => new ChooseAnswer(ChooseStatus.Decided, "m9", "model", Confidence: 0.9, Provider: "jev", DecisionRef: "decision-4"),
    };

    private static AgentStepRun Run(int attempt)
    {
        var address = InstanceAddress.From("T-9/step/" + attempt.ToString(CultureInfo.InvariantCulture));
        return new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From(attempt.ToString(CultureInfo.InvariantCulture) + ".1"), new StepRef("questions", "step.interview.questions"), WriteScope: false);
    }

    private async Task<PickRig> Rig(ModelPickSettings settings, CancellationToken ct, DrafterRan? drafter = null, AgentEntry[]? agents = null)
    {
        var launcher = new FakeLauncher();
        var judge = new FakeModelPickJudge();
        var sindri = _harness.Sindri(services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ILauncher>(launcher));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickJudge>(judge));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickSettings>(new FakeModelPickSettings { Settings = settings }));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickHistory>(new FakeModelPickHistory()));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelCatalog>(new FakeModelCatalog().With(_claude, _three).With(AgentId.From("codex"), _three)));
            _ = services.Replace(ServiceDescriptor.Singleton<IAgentCatalog>(new FixedAgentCatalog(agents ?? [AgentEntries.ClaudeCode])));   // one usable agent unless a test names more: the real catalog also has codex and cursor, which a challenger would move to
            if (drafter is not null)
            {
                _ = services.Replace(ServiceDescriptor.Singleton<ILaunchViews>(new DraftedLaunches(drafter)));
            }
        });
        await WorkflowsHarness.HoldLease(sindri, ct);
        var declared = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Repos: [new RepoEntry(Repo, Path.Combine(_harness.Home, "checkout"))], Workflow: new WorkflowSettings(Agent: new WorkflowAgentSettings(_claude)));
        var profile = ProfileRules.Check(declared).Value;
        var saved = await sindri.GetRequiredService<IApprovals>().Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), ct);
        Assert.False(saved.IsError, saved.IsError ? saved.Error.Message : null);
        var item = _harness.Fact(JsonSerializer.Serialize(new { id = Item, repo = Repo, spec = (string?)null }));
        return new PickRig(sindri, item, launcher, judge);
    }

    /// <summary>The drafter's launch: on claude-code, in the challenger's instance, with the model it asked for (null: not recorded).</summary>
    private sealed record DrafterRan(string? Model, AgentId? ChallengerAgent = null);

    /// <summary>The launches view with the one drafter launch every challenger Step of this file's addresses follows.</summary>
    private sealed class DraftedLaunches(DrafterRan drafter) : ILaunchViews
    {
        public ValueTask<Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success((LaunchRow?)null));

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken)
        {
            var row = new LaunchRow(LaunchId.From("LCH-" + new string('a', 32)), item, Repo: null, LaunchMode.Open, _claude, "engine", LaunchState.Started, DateTimeOffset.UnixEpoch, OutcomeAt: null,
                Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null, Ended: null,
                new LaunchStep(InstanceAddress.From("T-9/step/1"), "draft", Attempt: 1), Sequence: 1);
            var drafted = row with { Model = drafter.Model };
            if (drafter.ChallengerAgent is not { } ran)
            {
                return ValueTask.FromResult(Results.Success<IReadOnlyList<LaunchRow>>([drafted]));
            }

            // the challenger's own first launch, newest first: it was placed on another agent and the retry must stay there
            var first = drafted with { Id = AgentLaunches.LaunchIdFor(Challenge(1)), Target = ran, Model = null, Step = new LaunchStep(InstanceAddress.From("T-9/step/1"), "challenge", Attempt: 1) };
            return ValueTask.FromResult(Results.Success<IReadOnlyList<LaunchRow>>([first, drafted]));
        }

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> Since(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class PickRig(ServiceProvider sindri, FactValue item, FakeLauncher launcher, FakeModelPickJudge judge) : IAsyncDisposable
    {
        public FakeLauncher Launcher { get; } = launcher;

        public FakeModelPickJudge Judge { get; } = judge;

        public Task<AgentLaunched> Launch(AgentStepRun run, string? floor, CancellationToken ct, string? model = null, PreviousRun? previous = null, int escalation = 0, FactValue? workspace = null, StateAgent? agent = null) =>
            sindri.GetRequiredService<AgentLaunching>().Launch(new AgentLaunchRun(run, item, workspace, Model: model, Previous: previous, Escalation: escalation, Floor: floor, Agent: agent), ct);

        public async Task<IReadOnlyList<LedgerEvent>> Rows(CancellationToken ct) => (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 500, ct)).Value;

        /// <summary>Two fields of the 'model.picked' payload.</summary>
        public async Task<string> Picked(string first, string second, CancellationToken ct)
        {
            var row = (await Rows(ct)).First(e => e.Envelope.Type == "model.picked");
            using var document = JsonDocument.Parse(row.Envelope.Payload);
            return Text(document.RootElement, first) + "/" + Text(document.RootElement, second);
        }

        public ValueTask DisposeAsync() => sindri.DisposeAsync();

        private static string Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }
}
