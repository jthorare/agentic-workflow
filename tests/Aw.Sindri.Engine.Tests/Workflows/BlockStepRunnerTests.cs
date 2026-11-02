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
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;
using Aw.Sindri.Tests.Workflows.Inputs;

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
    private readonly MemoryInjectionTexts _texts = new();
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
    public async Task An_unreadable_item_repo_fails_the_run_and_never_uses_the_box_list()
    {
        await Approve(Profile(new WorkflowSettings(Judge: new JudgeSettings(Providers: ["claude-cli"]))));
        var run = await RunOf(repo: null) with { RepoUnreadable = true };

        var outcome = await Runner(_profiles).Run(run, TestContext.Current.CancellationToken);

        Assert.Equal("repo-unreadable", outcome.Failure);
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

    [Fact]
    public async Task A_block_Step_gets_the_injection_sections_and_the_typed_results_of_its_inputs()
    {
        var input = BlockId.From("input.test.a");
        var text = "notes for the Step";
        var typed = JsonSerializer.SerializeToElement(new { count = 2 });
        var recorded = new FilledInput(input, 1, [new InjectionEntry("notes", input, Encoding.UTF8.GetByteCount(text), StepInputRunner.Sha(text))], new InjectionResultEntry(typed.GetRawText().Length, StepInputRunner.Sha(typed.GetRawText())));
        _texts.Put(new InjectionKey("T-1/x/1", "route-evidence", "1.1", 1), new FilledTexts(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = text }, typed));
        var request = new FillInputRequest("T-1/x/1", "route-evidence", "1.1", 1, input, _judge, new Dictionary<string, WithValue>(StringComparer.Ordinal), new Dictionary<FactKey, FactValue>(), Repo: null);
        var run = (await RunOf(repo: null)) with { Fills = [new InjectionFill(recorded, request)] };

        var outcome = await Runner(_profiles).Run(run, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Failure);
        Assert.Equal(text, _seen!.Injection.Sections["notes"]);
        Assert.Equal(2, _seen.InputResults[input.Value].GetProperty("count").GetInt32());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Step_is_told_whether_the_instance_already_holds_a_heavy_slot(bool held)
    {
        var run = (await RunOf(repo: null)) with { HeavyHeld = held };

        _ = await Runner(_profiles).Run(run, TestContext.Current.CancellationToken);

        Assert.Equal(held, _seen!.HeavySlotHeld);
    }

    [Fact]
    public async Task A_block_Step_without_inputs_gets_no_sections_and_empty_results()
    {
        _ = await Runner(_profiles).Run(await RunOf(repo: null), TestContext.Current.CancellationToken);

        Assert.Empty(_seen!.Injection.Sections);
        Assert.Empty(_seen.InputResults);
    }

    [Fact]
    public async Task A_Step_gets_the_stored_content_of_every_fact_its_descriptor_reads()
    {
        var reads = new BlockDescriptor(BlockId.From("step.test.reads"), BlockKind.Step, Version: 1, [_change], [], [OutcomeName.From("done")], Capability.None, [], Rung: null, DeskCapable: false, Runtime: BlockRuntime.Block);
        var runner = new BlockStepRunner(
            new BlockStepRuntimes([new BlockStepRegistration(reads.Id)]),
            new BlockCatalog([reads], []),
            (_, context, _) =>
            {
                _seen = context;
                return ValueTask.FromResult(BlockStepResult.Completed(OutcomeName.From("done"), new Dictionary<FactKey, JsonElement>()));
            },
            _store,
            _store,
            _profiles,
            new StepContexts(_texts, new NoRunner(), new ItemReposStub()));
        var run = (await RunOf(repo: null)) with { Step = reads.Id };

        var outcome = await runner.Run(run, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Failure);
        Assert.Equal("Add a button", _seen!.Facts[_change].GetString());
    }

    [Fact]
    public async Task A_Step_that_reads_a_fact_the_instance_does_not_hold_fails_before_it_runs()
    {
        var run = (await RunOf(repo: null)) with { Step = ReadsStep.Id, Facts = new Dictionary<FactKey, FactValue>() };

        var outcome = await ReadsRunner().Run(run, TestContext.Current.CancellationToken);

        Assert.Contains("fact:change", outcome.Failure, StringComparison.Ordinal);
        Assert.Null(_seen);
    }

    [Theory]
    [InlineData(true, "web")]
    [InlineData(false, "Not A Repo")]
    public async Task A_repo_that_could_not_be_read_or_is_no_repo_name_fails_the_Step_before_it_runs(bool unreadable, string repo)
    {
        var run = (await RunOf(repo)) with { Step = ReadsStep.Id, RepoUnreadable = unreadable };

        var outcome = await ReadsRunner().Run(run, TestContext.Current.CancellationToken);

        Assert.Equal("repo-unreadable", outcome.Failure);
        Assert.Null(_seen);
    }

    [Fact]
    public async Task Withdraw_and_propose_fail_on_an_unreadable_repo_and_never_touch_the_proposals()
    {
        var proposals = new InMemoryScopeRunProposals();
        foreach (var step in new IBlockStep[] { new ScopeWithdrawStep(proposals), new ScopeProposeStep(proposals) })
        {
            var id = step is ScopeWithdrawStep ? ScopeWithdrawStep.Id : ScopeProposeStep.Id;
            var descriptor = ReadsStep.Descriptor with { Id = id, Inputs = [] };
            var runner = new BlockStepRunner(new BlockStepRuntimes([new BlockStepRegistration(id)]), new BlockCatalog([descriptor], []), (_, context, ct) => step.Run(context, ct), _store, _store, _profiles, new StepContexts(_texts, new NoRunner(), new ItemReposStub()));
            var run = (await RunOf("web")) with { Step = id, RepoUnreadable = true };

            var outcome = await runner.Run(run, TestContext.Current.CancellationToken);

            Assert.Equal("repo-unreadable", outcome.Failure);
        }

        Assert.Empty(proposals.Calls);
    }

    [Fact]
    public async Task The_Step_gets_the_instance_address_and_the_items_repo()
    {
        var run = (await RunOf("web")) with { Step = ReadsStep.Id };

        _ = await ReadsRunner().Run(run, TestContext.Current.CancellationToken);

        Assert.Equal(("T-1/x/1", "web"), (_seen!.Address, _seen.Repo!.Value.Value));
    }

    [Fact]
    public async Task A_child_instance_with_no_fact_item_gets_the_repo_of_the_item_its_address_starts_with()
    {
        var run = (await RunOf(repo: null)) with { Step = ReadsStep.Id, Address = "ITM-0123456789abcdef0123456789abcdef/scoping/1/idea-to-brief.3" };

        var outcome = await ReadsRunner(new ItemReposStub().With("ITM-0123456789abcdef0123456789abcdef", "web")).Run(run, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Failure);
        Assert.Equal("web", _seen!.Repo!.Value.Value);
    }

    [Fact]
    public async Task An_item_the_ledger_knows_no_repo_for_is_no_repo_and_an_unreadable_ledger_fails_the_Step()
    {
        var run = (await RunOf(repo: null)) with { Step = ReadsStep.Id, Address = "ITM-0123456789abcdef0123456789abcdef/scoping/1/idea-to-brief.3" };

        var none = await ReadsRunner(new ItemReposStub()).Run(run, TestContext.Current.CancellationToken);
        var seenRepo = _seen!.Repo;
        _seen = null;
        var failed = await ReadsRunner(new ItemReposStub(fails: true)).Run(run, TestContext.Current.CancellationToken);

        Assert.Null(none.Failure);
        Assert.Null(seenRepo);
        Assert.Equal("repo-unreadable", failed.Failure);
        Assert.Null(_seen);
    }

    private static class ReadsStep
    {
        internal static readonly BlockId Id = BlockId.From("step.test.reads");

        internal static BlockDescriptor Descriptor { get; } = new(Id, BlockKind.Step, Version: 1, [_change], [], [OutcomeName.From("done")], Capability.None, [], Rung: null, DeskCapable: false, Runtime: BlockRuntime.Block);
    }

    private BlockStepRunner ReadsRunner(ItemReposStub? repos = null) => new(
        new BlockStepRuntimes([new BlockStepRegistration(ReadsStep.Id, NeedsRepo: true)]),
        new BlockCatalog([ReadsStep.Descriptor], []),
        (_, context, _) =>
        {
            _seen = context;
            return ValueTask.FromResult(BlockStepResult.Completed(OutcomeName.From("done"), new Dictionary<FactKey, JsonElement>()));
        },
        _store,
        _store,
        _profiles,
        new StepContexts(_texts, new NoRunner(), repos ?? new ItemReposStub()));

    private static SindriProfile Profile(WorkflowSettings? workflow) => new(SindriProfile.CurrentSchemaVersion, "tester", Workflow: workflow);

    [Fact]
    public async Task The_approved_boxes_parallel_reaches_a_Step_that_takes_it()
    {
        await Approve(Profile(new WorkflowSettings(Judge: new JudgeSettings(Parallel: 6))));

        var outcome = await Runner(_profiles).Run(await ParallelRunOf(repo: null, ProfileParallel), TestContext.Current.CancellationToken);

        Assert.Null(outcome.Failure);
        Assert.Equal(6, _seen!.Arguments["parallel"].GetInt32());
    }

    [Fact]
    public async Task A_repos_parallel_wins_over_the_boxs()
    {
        await Approve(Profile(new WorkflowSettings(Judge: new JudgeSettings(Parallel: 6))) with { Repos = [new RepoEntry("app", "/a", Workflow: new RepoWorkflowSettings(Judge: new JudgeSettings(Parallel: 12)))] });

        _ = await Runner(_profiles).Run(await ParallelRunOf("app", ProfileParallel), TestContext.Current.CancellationToken);

        Assert.Equal(12, _seen!.Arguments["parallel"].GetInt32());
    }

    [Fact]
    public async Task An_unset_parallel_leaves_the_argument_out_so_the_Steps_default_applies()
    {
        await Approve(Profile(workflow: null));

        var outcome = await Runner(_profiles).Run(await ParallelRunOf(repo: null, ProfileParallel), TestContext.Current.CancellationToken);

        Assert.Null(outcome.Failure);
        Assert.False(_seen!.Arguments.ContainsKey("parallel"));
    }

    [Fact]
    public async Task A_literal_parallel_in_the_recipe_is_kept_over_the_profile()
    {
        await Approve(Profile(new WorkflowSettings(Judge: new JudgeSettings(Parallel: 6))));

        _ = await Runner(_profiles).Run(await ParallelRunOf(repo: null, new WithValue(JsonSerializer.SerializeToElement(3), Fact: null, Profile: null)), TestContext.Current.CancellationToken);

        Assert.Equal(3, _seen!.Arguments["parallel"].GetInt32());
    }

    private static WithValue ProfileParallel => new(Literal: null, Fact: null, Profile: "workflow.judge.parallel");

    private async Task<BlockStepRun> ParallelRunOf(string? repo, WithValue parallel)
    {
        var step = BlockId.From("step.scope.relevance-judge");
        var put = await _store.Put(Encoding.UTF8.GetBytes("{}"), TestContext.Current.CancellationToken);
        var held = new Dictionary<FactKey, FactValue>
        {
            [FactKey.From("fact:brief")] = put.Value,
            [FactKey.From("fact:lenses")] = put.Value,
            [FactKey.From("fact:relevanceVerdicts")] = put.Value,
        };
        var with = new Dictionary<string, WithValue>(StringComparer.Ordinal) { ["parallel"] = parallel };
        return new BlockStepRun("T-1/x/1", "relevance-judge", "1.1", step, with, held, repo);
    }

    private static ProfileHash Hash(char c) => ProfileHash.From(new string(c, 64));

    private string[] Providers() => [.. _seen!.Arguments["providers"].EnumerateArray().Select(static p => p.GetString()!)];

    private async Task Approve(SindriProfile profile) =>
        Assert.False((await _profiles.Save(new LoadedProfile(profile, Hash('2'), "/approved.yaml"), TestContext.Current.CancellationToken)).IsError);

    private BlockStepRunner Runner(IApprovedProfile approved, IFactStore? store = null, BlockStepResult? step = null) => new(
        new BlockStepRuntimes([new BlockStepRegistration(_judge), new BlockStepRegistration(BlockId.From("step.scope.relevance-judge"))]),
        EngineFixture.Catalog,
        (_, context, _) =>
        {
            _seen = context;
            return ValueTask.FromResult(step ?? BlockStepResult.Completed(OutcomeName.From("ui"), new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:decision")] = JsonSerializer.SerializeToElement(new { choice = "ui" }) }));
        },
        _store,
        store ?? _store,
        approved,
        new StepContexts(_texts, new NoRunner(), new ItemReposStub()));

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
        return new BlockStepRun("T-1/x/1", "route-evidence", "1.1", _judge, with, new Dictionary<FactKey, FactValue> { [_change] = put.Value }, repo);
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
