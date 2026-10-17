using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>What the 'RunBlockStep' activity does (DT spec §8.2): the providers come from the approved profile, a '{ fact }' argument reads the stored content, and every error path becomes a failed outcome.</summary>
[Lane(TestLane.Unit)]
public sealed class BlockStepRunnerTests
{
    private static readonly BlockId _judge = BlockId.From("step.judge.choose");
    private static readonly FactKey _change = FactKey.From("fact:change");
    private static readonly string[] _choices = ["ui", "api"];
    private static readonly string[] _factNames = ["fact:change"];
    private static readonly string[] _defaults = ["claude-cli", "codex-cli", "cursor-cli", "rules"];

    private readonly InMemoryFactStore _store = new();
    private readonly InMemoryProfiles _profiles = new();
    private StepContext? _seen;

    [Fact]
    public async Task A_live_only_edit_that_adds_jev_does_not_reach_the_Step()
    {
        _profiles.Live = new LoadedProfile(Profile(new WorkflowSettings(Judge: new JudgeSettings(Providers: ["jev", "claude-cli"]))), Hash('1'), "/live.yaml");

        var outcome = await Runner(_profiles).Run(await RunOf(repo: null), TestContext.Current.CancellationToken);

        Assert.Null(outcome.Failure);
        Assert.Equal(_defaults, Providers());
    }

    [Fact]
    public async Task The_approved_profiles_providers_reach_the_Step_and_the_live_file_is_not_read()
    {
        await Approve(Profile(new WorkflowSettings(Judge: new JudgeSettings(Providers: ["jev", "codex-cli"]))));
        _profiles.Live = SindriErrors.ProfileInvalid.With("the live file is broken");

        _ = await Runner(_profiles).Run(await RunOf(repo: null), TestContext.Current.CancellationToken);

        Assert.Equal(["jev", "codex-cli", "rules"], Providers());
    }

    [Fact]
    public async Task A_repo_list_of_rules_stays_rules_when_the_live_file_is_broken()
    {
        await Approve(Profile(workflow: null) with { Repos = [new RepoEntry("app", "/a", Workflow: new RepoWorkflowSettings(Judge: new JudgeSettings(Providers: ["rules"])))] });
        _profiles.Live = SindriErrors.ProfileInvalid.With("the live file is broken");

        _ = await Runner(_profiles).Run(await RunOf("app"), TestContext.Current.CancellationToken);

        Assert.Equal(["rules"], Providers());
    }

    [Fact]
    public async Task An_unreadable_approved_profile_fails_the_run_and_never_runs_the_Step()
    {
        var outcome = await Runner(new UnreadableProfile()).Run(await RunOf(repo: null), TestContext.Current.CancellationToken);

        Assert.StartsWith("profile-unreadable: ", outcome.Failure, StringComparison.Ordinal);
        Assert.Null(_seen);
    }

    [Fact]
    public async Task A_fact_argument_reads_the_stored_content()
    {
        var run = await RunOf(repo: null, prompt: new WithValue(Literal: null, _change, Profile: null));

        _ = await Runner(_profiles).Run(run, TestContext.Current.CancellationToken);

        Assert.Equal("Add a button", _seen!.Arguments["prompt"].GetString());
    }

    [Fact]
    public async Task A_fact_that_cannot_be_read_fails_the_run_naming_the_fact()
    {
        var run = await RunOf(repo: null) with { Facts = new Dictionary<FactKey, FactValue> { [_change] = new FactValue("fact:sha256:" + new string('9', 64), new string('9', 64), HeadSha: null) } };

        var outcome = await Runner(_profiles).Run(run, TestContext.Current.CancellationToken);

        Assert.Contains("fact:change can't be read", outcome.Failure, StringComparison.Ordinal);
        Assert.Null(_seen);
    }

    [Fact]
    public async Task An_output_that_cannot_be_stored_fails_the_run()
    {
        var runner = Runner(_profiles, new FailingStore());

        var outcome = await runner.Run(await RunOf(repo: null), TestContext.Current.CancellationToken);

        Assert.Equal("disk full", outcome.Failure);
        Assert.Null(outcome.Outcome);
    }

    [Fact]
    public async Task A_Step_that_returns_Failed_is_a_failed_outcome_with_its_reason()
    {
        var runner = Runner(_profiles, step: BlockStepResult.Failed("no provider answered"));

        var outcome = await runner.Run(await RunOf(repo: null), TestContext.Current.CancellationToken);

        Assert.Equal("no provider answered", outcome.Failure);
        Assert.Null(outcome.Outputs);
    }

    private static SindriProfile Profile(WorkflowSettings? workflow) => new(SindriProfile.CurrentSchemaVersion, "tester", Workflow: workflow);

    private static ProfileHash Hash(char c) => ProfileHash.From(new string(c, 64));

    private string[] Providers() => [.. _seen!.Arguments["providers"].EnumerateArray().Select(static p => p.GetString()!)];

    private async Task Approve(SindriProfile profile) =>
        Assert.False((await _profiles.Save(new LoadedProfile(profile, Hash('2'), "/approved.yaml"), TestContext.Current.CancellationToken)).IsError);

    private BlockStepRunner Runner(IApprovedProfile approved, IFactStore? store = null, BlockStepResult? step = null) => new(
        new BlockStepRuntimes([new BlockStepRegistration(_judge)]),
        EngineFixture.Catalog,
        (_, context, _) =>
        {
            _seen = context;
            return ValueTask.FromResult(step ?? BlockStepResult.Completed(OutcomeName.From("ui"), new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:decision")] = JsonSerializer.SerializeToElement(new { choice = "ui" }) }));
        },
        _store,
        store ?? _store,
        approved);

    private async Task<BlockStepRun> RunOf(string? repo, WithValue? prompt = null)
    {
        var put = await _store.Put(Encoding.UTF8.GetBytes("Add a button"), TestContext.Current.CancellationToken);
        var with = new Dictionary<string, WithValue>(StringComparer.Ordinal)
        {
            ["prompt"] = prompt ?? new WithValue(JsonSerializer.SerializeToElement("pick"), Fact: null, Profile: null),
            ["choices"] = new WithValue(JsonSerializer.SerializeToElement(_choices), Fact: null, Profile: null),
            ["facts"] = new WithValue(JsonSerializer.SerializeToElement(_factNames), Fact: null, Profile: null),
            ["providers"] = new WithValue(Literal: null, Fact: null, Profile: "workflow.judge.providers"),
        };
        return new BlockStepRun("T-1/workflow.x/1", "route-evidence", "1.1", _judge, with, new Dictionary<FactKey, FactValue> { [_change] = put.Value }, repo);
    }

    private sealed class UnreadableProfile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<LoadedProfile?>>(SindriErrors.ApprovalsUnavailable.With("the approvals file is locked"));
    }

    private sealed class FailingStore : IFactStore
    {
        public ValueTask<Result<FactValue>> Put(ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<FactValue>>(SindriErrors.ApiInternal.With("disk full"));
    }
}
