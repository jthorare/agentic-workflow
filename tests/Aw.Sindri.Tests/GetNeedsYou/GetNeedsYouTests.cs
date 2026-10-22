using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApproveAll;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Requests;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetNeedsYou;

/// <summary>The human's list (API spec §3.1, §3.6; UI spec E-7, E-16): urgency, then since, then id; a session's repo scope; the empty sentence with the counts.</summary>
[Lane(TestLane.Unit)]
public sealed class GetNeedsYouTests
{
    private readonly RequestQueryHarness _q = new();

    private readonly FakeOnyxNotices _onyx = new();

    private readonly InMemoryJobViews _jobs = new();
    private IShippedRecipes _shipped = new FakeShippedRecipes([]);

    private GetNeedsYouService Service() => new(_q.Views.Views, _q.Views.Progress, _q.Views.Slice.Callers, new NeedsYouLiveRows(_onyx, new SetupNotices(_jobs, _q.Views.Slice.Profiles, new ApproveAllPlanReader(_q.Views.Slice.Profiles, _shipped), new PassThroughScrubber(), TimeProvider.System), new PassThroughScrubber(), TimeProvider.System), TimeProvider.System, new NeedsYouJoins(new SomeInstances(), new SomeInstances(), new Core.Workflow.Registry.RecipeRegistryReader(new Testing.Workflow.Registry.InMemoryRecipeStore(), new Testing.Workflow.Registry.InMemoryRecipeApprovals(), EngineFixture.Catalog, SliceHarness.Json, new NoProfileFacts()), EngineFixture.Catalog));

    private async Task<GetNeedsYouResponse> Get() => (await Service().Handle(A.GetNeedsYouRequest().Build(), RequestQueryHarness.Token)).Value;

    [Fact]
    public async Task GetNeedsYou_shows_the_managed_onyx_row_to_a_human_and_not_to_a_session()
    {
        _onyx.Notice = new Core.Onyx.OnyxNotice("Set up Onyx", "Onyx isn't set up on this box", "sindri onyx up");

        using (_q.Human())
        {
            var row = Assert.Single((await Get()).Rows);
            Expect.Equal("failure:service:onyx", row.Id.Value);
            Expect.Equal("sindri onyx up", row.Cli);
        }

        using (_q.Session(repo: null))
        {
            Assert.Empty((await Get()).Rows);
        }
    }

    private void AddRepoJob(JobState state, string? summary = null, int n = 1) =>
        _jobs.Put(new JobRow(JobId.From("JOB-" + n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), JobKind.From("AddRepo"), JobKey.From("a" + n.ToString(System.Globalization.CultureInfo.InvariantCulture)), Item: null, Repo: null, Service: null, state, "human:app",
            KnownInstants.Epoch.AddMinutes(n), state == JobState.Running ? null : KnownInstants.Epoch.AddMinutes(n + 1), [], 0, summary, state == JobState.Failed ? "SND-SETUP-001" : null, Result: null, CancelledBy: null, Sequence: n));

    [Fact]
    public async Task A_failed_AddRepo_job_with_no_repo_in_the_profile_is_the_setup_repo_row_for_a_human()
    {
        AddRepoJob(JobState.Failed, "That folder is not a git checkout (fix: pick the repo's top folder)");
        using var entered = _q.Human();

        var row = Assert.Single((await Get()).Rows);

        Expect.Equal(("setup:repo", NeedsYouKind.Setup, Urgency.Colour, "AddRepo"), (row.Id.Value, row.Kind, row.Urgency, row.PrimaryAction!.Value.Value));
        Expect.Equal("sindri repo add <path>", row.Cli);
        Expect.True(row.Reason.Contains("pick the repo's top folder", StringComparison.Ordinal));
        Expect.Equal(KnownInstants.Epoch.AddMinutes(2), row.Since);
    }

    [Fact]
    public async Task The_setup_repo_row_is_not_a_sessions()
    {
        AddRepoJob(JobState.Failed);
        using var entered = _q.Session(repo: null);

        Assert.Empty((await Get()).Rows);
    }

    [Fact]
    public async Task A_new_AddRepo_job_running_or_succeeded_clears_the_setup_repo_row()
    {
        AddRepoJob(JobState.Failed, n: 1);
        using var entered = _q.Human();
        _ = Assert.Single((await Get()).Rows);

        AddRepoJob(JobState.Running, n: 3);
        Assert.Empty((await Get()).Rows);

        AddRepoJob(JobState.Succeeded, n: 3);
        Assert.Empty((await Get()).Rows);
    }

    [Fact]
    public async Task Once_the_profile_names_a_repo_the_step_is_done_and_the_row_clears()
    {
        AddRepoJob(JobState.Failed);
        _q.Views.Slice.Profiles.Live = SetupFactsRigProfile();
        using var entered = _q.Human();

        Assert.Empty((await Get()).Rows);
    }

    [Fact]
    public async Task A_failure_with_no_sentence_still_says_what_to_do()
    {
        AddRepoJob(JobState.Failed, summary: null);
        using var entered = _q.Human();

        var row = Assert.Single((await Get()).Rows);

        Expect.True(row.Reason.Contains("Pick the repo's top folder", StringComparison.Ordinal));
    }

    private static Result<Core.Profile.LoadedProfile> SetupFactsRigProfile() => SetupSources.SetupFactsRig.ProfileWithRepos();

    [Fact]
    public async Task GetNeedsYou_shows_the_setup_recipes_row_and_the_own_approval_rows_to_a_human_and_not_to_a_session()
    {
        _ = await _q.Views.Slice.Profiles.Save(SliceHarness.Profile(), RequestQueryHarness.Token);
        var fake = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem());
        fake.OwnApproval.Add(ApproveAllFixtures.ShipNeedsItsOwn());
        _shipped = fake;

        using (_q.Human())
        {
            var rows = (await Get()).Rows;
            Expect.Sequence(["recipe:workflow.ship", "setup:recipes"], [.. rows.Select(static r => r.Id.Value)]);
            var setup = rows.Single(static r => r.Kind == NeedsYouKind.Setup);
            Expect.Equal(("Approve all new workflows (2)", "ApproveAllRecipes", "sindri recipe approve-all"), (setup.Title, setup.PrimaryAction?.Value, setup.Cli));
            var recipe = rows.Single(static r => r.Kind == NeedsYouKind.Recipe);
            Expect.Equal(("Approve a changed workflow: Ship a change", "ApproveRecipe", "sindri recipe approve workflow.ship"), (recipe.Title, recipe.PrimaryAction?.Value, recipe.Cli));
        }

        using (_q.Session(repo: null))
        {
            Assert.Empty((await Get()).Rows);
        }
    }

    [Fact]
    public async Task GetNeedsYou_has_no_recipe_rows_when_no_workflow_waits()
    {
        _ = await _q.Views.Slice.Profiles.Save(SliceHarness.Profile(), RequestQueryHarness.Token);
        using var entered = _q.Human();

        Assert.Empty((await Get()).Rows);
    }

    [Fact]
    public async Task GetNeedsYou_drops_only_the_recipe_rows_when_the_plan_cannot_be_read()
    {
        await _q.Drift();
        _shipped = new UnreadableShippedRecipes(throws: false);
        using var entered = _q.Human();
        _ = await _q.Views.Slice.Profiles.Save(SliceHarness.Profile(), RequestQueryHarness.Token);

        Expect.Sequence(["profile:drift"], [.. (await Get()).Rows.Select(static r => r.Id.Value)]);

        _shipped = new UnreadableShippedRecipes(throws: true);

        Expect.Sequence(["profile:drift"], [.. (await Get()).Rows.Select(static r => r.Id.Value)]);
    }

    [Fact]
    public async Task GetNeedsYou_has_no_recipe_rows_before_a_profile_is_approved()
    {
        _shipped = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem());
        using var entered = _q.Human();

        Assert.Empty((await Get()).Rows);
    }

    [Fact]
    public async Task GetNeedsYou_lists_rows_by_urgency_then_since_then_id()
    {
        await _q.Drift();
        await _q.Ran("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001");
        var first = await _q.File("first");
        var second = await _q.File("second");
        using var entered = _q.Human();

        var result = await Service().Handle(A.GetNeedsYouRequest().Build(), RequestQueryHarness.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Sequence(["decision:" + first.Value, "decision:" + second.Value, "failure:service:heartbeat", "profile:drift"], [.. result.Value.Rows.Select(r => r.Id.Value)]);
        Expect.Sequence([NeedsYouKind.Decision, NeedsYouKind.Decision, NeedsYouKind.Failure, NeedsYouKind.Profile], [.. result.Value.Rows.Select(r => r.Kind)]);
        Assert.Null(result.Value.Empty);
        Expect.Equal(_q.Views.Progress.Current.AsOf, result.Value.AsOf);
    }

    [Fact]
    public async Task A_decision_row_names_its_label_item_actions_and_cli()
    {
        var item = RequestQueryHarness.Item('1');
        await _q.CreateItem(item, "web");
        var decision = await _q.File("Split the work.", repo: "web", item: item);
        using var entered = _q.Human();

        var row = Assert.Single((await Get()).Rows);

        Expect.Equal(("decision · re-scope", item, true, EmptyText.RecordedOnly()), (row.Label, row.Item!.Value, row.RecordedOnly, row.Notice));
        Expect.Equal(($"sindri decide {decision.Value} accept", "Decide"), (row.Cli, row.PrimaryAction?.Value));
        Assert.Contains(row.PrimaryAction!.Value, row.Actions);
    }

    [Fact]
    public async Task Session_sees_only_its_repo_rows()
    {
        _ = await _q.File("mine", repo: "alpha");
        _ = await _q.File("theirs", repo: "beta");
        await _q.Ran("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001");
        using var entered = _q.Session("alpha");

        var rows = (await Get()).Rows;

        Expect.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.Repo?.Value == "beta");
        Assert.Contains(rows, r => r.Kind == NeedsYouKind.Failure);
    }

    [Fact]
    public async Task Needs_you_empty_sentence_counts_running_and_queued()
    {
        await _q.CreateItem(RequestQueryHarness.Item('1'));
        await _q.CreateItem(RequestQueryHarness.Item('2'));
        await _q.CreateItem(RequestQueryHarness.Item('3'));
        await _q.Start(RequestQueryHarness.Item('3'));
        using var entered = _q.Human();

        var response = await Get();

        Assert.Empty(response.Rows);
        Expect.Equal(EmptyText.NeedsYou(running: 1, queued: 2), response.Empty);
    }

    [Fact]
    public async Task Stalled_views_replace_the_sentence_and_keep_the_rows()
    {
        _ = await _q.File("kept");
        _q.Views.Publish(stalledAt: 1);
        using var entered = _q.Human();

        var response = await Get();

        Expect.Equal(EmptyText.Stalled(1), response.Empty);
        _ = Assert.Single(response.Rows);
    }

    [Fact]
    public async Task A_decided_request_leaves_the_list()
    {
        var decision = await _q.File("one");
        await _q.Decide(decision, "decline");
        using var entered = _q.Human();

        Assert.Empty((await Get()).Rows);
    }

    [Fact]
    public async Task Every_cli_is_a_filled_CliForm()
    {
        await _q.Drift();
        await _q.Ran("heartbeat", ServiceOutcome.Failed);
        _ = await _q.File("one");
        using var entered = _q.Human();

        var rows = (await Get()).Rows;

        Expect.Equal(3, rows.Count);
        foreach (var row in rows)
        {
            Assert.Matches("^sindri( [A-Za-z0-9][A-Za-z0-9:._-]*)+$", row.Cli);
            Assert.DoesNotContain('<', row.Cli);
            Assert.StartsWith("sindri ", row.Cli, StringComparison.Ordinal);
            Assert.Contains(row.PrimaryAction!.Value, row.Actions);
            Assert.StartsWith(row.Kind.ToString(), row.Label, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Without_a_caller_it_is_refused()
    {
        var result = await Service().Handle(A.GetNeedsYouRequest().Build(), RequestQueryHarness.Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    private sealed class UnreadableShippedRecipes(bool throws) : IShippedRecipes
    {
        public ValueTask<Result<ShippedRecipePlan>> Plan(CancellationToken cancellationToken) =>
            throws ? throw new InvalidOperationException("the registry is unreadable") : ValueTask.FromResult<Result<ShippedRecipePlan>>(SindriErrors.ProfileNotApproved.With("the registry is unreadable"));

        public ValueTask<Result<ApproveAllResult>> Approve(RedeemedPlan plan, IProgress<string> progress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoProfileFacts : Core.Workflow.Validation.IRecipeProfileFacts
    {
        public ValueTask<Result<Core.Workflow.Validation.RecipeProfileFacts>> Current(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
