using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.GetSetup;

/// <summary>The pure decision of each setup step: every state it can be in, the plain words, and the action each state points at.</summary>
public sealed class SetupStepsTests
{
    private static RepoFact Repos(params string[] names) =>
        new(names.Length) { Items = new SetupItems([.. names.Select(static n => new SetupItem(n, SetupState.Done, Detail: null))]) };

    [Fact, Trait("Lane", "unit")]
    public void Repo_is_the_owners_with_AddRepo_until_the_profile_names_one()
    {
        var first = SetupSteps.Repo(new RepoFact(0));
        var done = SetupSteps.Repo(Repos("demo", "other"));

        Assert.Equal((SetupState.NeedsYou, SetupActions.AddRepo, "Pick the folder of a repo to start."), (first.State, first.Action, first.Why));
        Assert.Equal((SetupState.Done, null, "demo, other"), (done.State, done.Action, done.Detail));
        Assert.Equal(["demo", "other"], done.Items.Select(static i => i.Repo), StringComparer.Ordinal);
        Assert.Equal("1 repository", SetupSteps.Repo(new RepoFact(1)).Detail);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ProfilePresence.Missing, SetupState.Todo, null)]
    [InlineData(ProfilePresence.Unapproved, SetupState.NeedsYou, SetupActions.ApproveProfile)]
    [InlineData(ProfilePresence.Drifted, SetupState.NeedsYou, SetupActions.ApproveProfile)]
    [InlineData(ProfilePresence.Approved, SetupState.Done, null)]
    public void Profile_asks_the_human_when_it_is_waiting_or_has_changed(ProfilePresence presence, SetupState state, string? action)
    {
        var step = SetupSteps.Profile(new ProfileFact(presence), Repos("demo"));

        Assert.Equal((state, action), (step.State, step.Action));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ProfilePresence.Unapproved)]
    [InlineData(ProfilePresence.Approved)]
    [InlineData(ProfilePresence.Drifted)]
    public void Profile_waits_for_a_first_repo_whatever_the_file_says(ProfilePresence presence)
    {
        var step = SetupSteps.Profile(new ProfileFact(presence), new RepoFact(0));

        Assert.Equal((SetupState.Todo, null, "Waits for your first repo."), (step.State, step.Action, step.Why));
    }

    [Fact, Trait("Lane", "unit")]
    public void Profile_names_the_repo_the_owner_added_when_it_waits_for_approval()
    {
        var step = SetupSteps.Profile(new ProfileFact(ProfilePresence.Unapproved), Repos("demo"));

        Assert.StartsWith("You added demo. ", step.Why, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Profile_carries_the_preview_it_was_given_and_none_without_one()
    {
        SetupPreviewLine[] lines = [new("repo", "Works in demo (/work/demo).")];

        var with = SetupSteps.Profile(new ProfileFact(ProfilePresence.Unapproved) { Preview = lines }, Repos("demo"));
        var without = SetupSteps.Profile(new ProfileFact(ProfilePresence.Missing), Repos("demo"));

        Assert.Equal(lines, with.Preview);
        Assert.Null(without.Preview);
    }

    [Fact, Trait("Lane", "unit")]
    public void Recipes_wait_for_an_approved_profile_then_count_what_waits()
    {
        var none = SetupSteps.Recipes(new RecipesFact(0, 0), profileApproved: true);
        var early = SetupSteps.Recipes(new RecipesFact(26, 0), profileApproved: false);
        var waiting = SetupSteps.Recipes(new RecipesFact(26, 20), profileApproved: true);
        var done = SetupSteps.Recipes(new RecipesFact(26, 26), profileApproved: true);

        Assert.Equal((SetupState.Todo, null), (none.State, none.Action));
        Assert.Equal((SetupState.Todo, null, null, "After the profile is approved.", "26 workflows registered."), (early.State, early.Action, early.Progress, early.Why, early.Detail));
        Assert.Equal((SetupState.NeedsYou, SetupActions.ApproveAllRecipes, new SetupProgress(20, 26, "workflows"), "6 workflows wait for your approval."), (waiting.State, waiting.Action, waiting.Progress, waiting.Why));
        Assert.Equal((SetupState.Done, null, new SetupProgress(26, 26, "workflows")), (done.State, done.Action, done.Progress));
        Assert.Equal("1 workflow waits for your approval.", SetupSteps.Recipes(new RecipesFact(3, 2), profileApproved: true).Why);
    }

    [Fact, Trait("Lane", "unit")]
    public void Mcp_needs_an_installed_provider_first_then_waits_for_a_repo_and_is_done_when_every_repo_is_registered()
    {
        var noProvider = SetupSteps.Mcp(new McpFact(2, 2, Providers: string.Empty));
        var waiting = SetupSteps.Mcp(new McpFact(0, 0, "Claude Code"));
        var todo = SetupSteps.Mcp(new McpFact(1, 0, "Claude Code"));
        var some = SetupSteps.Mcp(new McpFact(3, 1, "Claude Code"));
        var done = SetupSteps.Mcp(new McpFact(3, 3, "Claude Code, Codex"));

        Assert.Equal((SetupState.Todo, null, "Install Claude Code, Codex or Cursor."), (noProvider.State, noProvider.Action, noProvider.Why));
        Assert.Equal((SetupState.Todo, null, "Waits for an approved profile with a repo."), (waiting.State, waiting.Action, waiting.Why));
        Assert.Equal((SetupState.Todo, SetupActions.RegisterMcp, null), (todo.State, todo.Action, todo.Progress));
        Assert.Equal((SetupState.Todo, SetupActions.RegisterMcp, new SetupProgress(1, 3, "repos")), (some.State, some.Action, some.Progress));
        Assert.Equal((SetupState.Done, null), (done.State, done.Action));
    }

    [Fact, Trait("Lane", "unit")]
    public void Map_runs_on_its_own_so_it_never_has_an_action_and_its_items_follow_the_fact()
    {
        var line = new SetupItems([new SetupItem("demo", SetupState.Running, Detail: null)]);
        var steps = new[]
        {
            SetupSteps.Map(new MapFact(MapPhase.Waiting)),
            SetupSteps.Map(new MapFact(MapPhase.NotBuilt)),
            SetupSteps.Map(new MapFact(MapPhase.Building) { Items = line }),
            SetupSteps.Map(new MapFact(MapPhase.Building, new SetupProgress(40, 100, "files"))),
            SetupSteps.Map(new MapFact(MapPhase.Failed, FailureCode: "SND-CONTEXT-040")),
            SetupSteps.Map(new MapFact(MapPhase.Built)),
        };

        Assert.Equal([SetupState.Todo, SetupState.Todo, SetupState.Running, SetupState.Running, SetupState.Failed, SetupState.Done], steps.Select(static s => s.State));
        Assert.All(steps, static s => Assert.Null(s.Action));
        Assert.Equal(new SetupProgress(40, 100, "files"), steps[3].Progress);
        Assert.Equal("40 of 100 done.", steps[3].Detail);
        Assert.Equal("Sindri is mapping demo.", steps[2].Why);
        Assert.Equal("Waits for an approved profile with a repo.", steps[0].Why);
        Assert.Equal(["demo"], steps[2].Items.Select(static i => i.Repo), StringComparer.Ordinal);
        Assert.Equal("SND-CONTEXT-040", steps[4].Error?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Onyx_is_done_when_up_todo_when_not_started_skipped_when_this_machine_cannot_run_it_and_failed_when_it_does_not_answer()
    {
        var done = SetupSteps.Onyx(new OnyxFact(OnyxState.Running, Unreachable: false, BlockedByCode: null));
        var notSetUp = SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null));
        var stopped = SetupSteps.Onyx(new OnyxFact(OnyxState.Stopped, Unreachable: false, BlockedByCode: null));
        var noDocker = SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: "SND-ONYX-001"));
        var unknown = SetupSteps.Onyx(new OnyxFact(OnyxState.Unknown, Unreachable: false, BlockedByCode: null));
        var memory = SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: "SND-ONYX-003"));
        var silent = SetupSteps.Onyx(new OnyxFact(OnyxState.Running, Unreachable: true, BlockedByCode: null));

        Assert.Equal(SetupState.Done, done.State);
        Assert.Equal((SetupState.Todo, SetupActions.SetupOnyx), (notSetUp.State, notSetUp.Action));
        Assert.Equal((SetupState.Todo, "Onyx is set up but stopped."), (stopped.State, stopped.Detail));
        Assert.Equal((SetupState.Skipped, null, null, "Docker isn't installed.", "Install Docker to set up Onyx: https://docs.docker.com/get-docker/"), (noDocker.State, noDocker.Error, noDocker.Action, noDocker.Why, noDocker.Detail));
        Assert.Equal((SetupState.Failed, null, "Sindri couldn't ask Docker about Onyx."), (unknown.State, unknown.Action, unknown.Why));
        Assert.Equal((SetupState.Skipped, null, null), (memory.State, memory.Error, memory.Action));
        Assert.Equal((SetupState.Failed, "SND-ONYX-005"), (silent.State, silent.Error?.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_step_keeps_its_code_out_of_every_plain_text()
    {
        List<SetupStep> steps =
        [
            SetupSteps.Onyx(new OnyxFact(OnyxState.Running, Unreachable: true, BlockedByCode: null)),
            SetupSteps.Map(new MapFact(MapPhase.Failed, FailureCode: "SND-CONTEXT-040")),
            .. Enum.GetValues<SetupStepId>().Select(static id => SetupSteps.Unreadable(id, "SND-LEDGER-002")),
        ];

        Assert.All(steps, static s =>
        {
            Assert.Equal(SetupState.Failed, s.State);
            Assert.DoesNotContain("SND-", s.Title + s.Why + s.Detail + s.Error?.Sentence, StringComparison.Ordinal);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_step_has_a_title_and_a_reason_in_every_state()
    {
        var all = new[]
        {
            SetupSteps.Repo(new RepoFact(0)), SetupSteps.Profile(new ProfileFact(ProfilePresence.Missing), new RepoFact(0)), SetupSteps.Recipes(new RecipesFact(0, 0), profileApproved: false),
            SetupSteps.Mcp(new McpFact(0, 0, "Claude Code")), SetupSteps.Map(new MapFact(MapPhase.Waiting)), SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null)),
        };

        Assert.Equal(Enum.GetValues<SetupStepId>(), all.Select(static s => s.Id));
        Assert.All(all, static s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Title));
            Assert.EndsWith(".", s.Why, StringComparison.Ordinal);
        });
    }
}
