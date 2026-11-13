using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

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
    [InlineData(ProfilePresence.Missing, SetupState.Todo)]
    [InlineData(ProfilePresence.Present, SetupState.Done)]
    public void Profile_is_done_once_the_file_is_there(ProfilePresence presence, SetupState state)
    {
        var step = SetupSteps.Profile(new ProfileFact(presence), Repos("demo"));

        Assert.Equal((state, null), (step.State, step.Action));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ProfilePresence.Present)]
    [InlineData(ProfilePresence.Missing)]
    public void Profile_waits_for_a_first_repo_whatever_the_file_says(ProfilePresence presence)
    {
        var step = SetupSteps.Profile(new ProfileFact(presence), new RepoFact(0));

        Assert.Equal((SetupState.Todo, null, "Waits for your first repo."), (step.State, step.Action, step.Why));
    }

    [Fact, Trait("Lane", "unit")]
    public void Profile_carries_the_preview_it_was_given_and_none_without_one()
    {
        SetupPreviewLine[] lines = [new("repo", "Works in demo (/work/demo).")];

        var with = SetupSteps.Profile(new ProfileFact(ProfilePresence.Present) { Preview = lines }, Repos("demo"));
        var without = SetupSteps.Profile(new ProfileFact(ProfilePresence.Missing), Repos("demo"));

        Assert.Equal(lines, with.Preview);
        Assert.Null(without.Preview);
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
        Assert.Equal((SetupState.Todo, null, "Waits for a profile with a repo."), (waiting.State, waiting.Action, waiting.Why));
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
        Assert.Equal("Waits for a profile with a repo.", steps[0].Why);
        Assert.Equal(["demo"], steps[2].Items.Select(static i => i.Repo), StringComparer.Ordinal);
        Assert.Equal("SND-CONTEXT-040", steps[4].Error?.Code);
    }

    // #3112: the step carries the stage it is in, with or without a count, so the CLI and the app word "indexing" and "mapping" from one place (the words are api's).
    [Theory, Trait("Lane", "unit")]
    [InlineData(MapStage.Index)]
    [InlineData(MapStage.Map)]
    public void Map_building_puts_the_stage_on_the_step_next_to_its_count(MapStage stage)
    {
        var step = SetupSteps.Map(new MapFact(MapPhase.Building, new SetupProgress(7000, 15223, "files")) { Stage = stage });

        Assert.Equal((SetupState.Running, stage), (step.State, step.Stage));
        Assert.Equal(new SetupProgress(7000, 15223, "files"), step.Progress);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(MapStage.Index)]
    [InlineData(MapStage.Map)]
    public void Map_building_with_no_count_yet_still_has_the_stage(MapStage stage)
    {
        var step = SetupSteps.Map(new MapFact(MapPhase.Building, Progress: null) { Stage = stage });

        Assert.Equal((SetupState.Running, stage, (SetupProgress?)null), (step.State, step.Stage, step.Progress));
    }

    // #3112: a failed build keeps the stage it failed in, so the step can say which build failed.
    [Theory, Trait("Lane", "unit")]
    [InlineData(MapStage.Index)]
    [InlineData(MapStage.Map)]
    public void Map_failed_keeps_the_stage_it_failed_in(MapStage stage)
    {
        var step = SetupSteps.Map(new MapFact(MapPhase.Failed, FailureCode: "SND-CODEIDX-010") { Stage = stage });

        Assert.Equal((SetupState.Failed, stage), (step.State, step.Stage));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_built_map_has_no_stage()
    {
        Assert.Null(SetupSteps.Map(new MapFact(MapPhase.Built, Progress: null)).Stage);
    }

    [Fact, Trait("Lane", "unit")]
    public void Sources_with_none_named_is_the_humans_and_says_no_source_is_connected_yet()
    {
        var step = SetupSteps.Sources(new SourcesFact(0));

        Assert.Equal((SetupStepId.Sources, "Connect your sources", SetupState.Todo, "No source is connected yet.", SetupActions.ConnectIndexSource), (step.Id, step.Title, step.State, step.Why, step.Action));
        Assert.Empty(step.Items);
        Assert.Null(step.Error);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("github", "GitHub needs a token.")]
    [InlineData("slack", "Slack needs a token.")]
    [InlineData("linear", "Linear needs a token.")]
    public void Sources_names_the_source_that_needs_a_token_and_lists_it(string source, string why)
    {
        var step = SetupSteps.Sources(InMemorySetupFacts.NamedSources((source, false)));

        Assert.Equal((SetupState.Todo, SetupActions.ConnectIndexSource, why), (step.State, step.Action, step.Why));
        Assert.Equal([(source, SetupState.Todo, "needs token")], step.Items.Select(static i => (i.Repo, i.State, i.Detail)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Sources_names_only_the_sources_still_missing_a_token_and_keeps_a_line_for_each_named_one()
    {
        var both = SetupSteps.Sources(InMemorySetupFacts.NamedSources(("github", false), ("slack", false)));
        var one = SetupSteps.Sources(InMemorySetupFacts.NamedSources(("github", true), ("slack", false)));

        Assert.Equal(("GitHub and Slack need a token.", SetupState.Todo), (both.Why, both.State));
        Assert.Equal(("Slack needs a token.", SetupState.Todo, SetupActions.ConnectIndexSource), (one.Why, one.State, one.Action));
        Assert.Equal([("github", SetupState.Done, "connected"), ("slack", SetupState.Todo, "needs token")], one.Items.Select(static i => (i.Repo, i.State, i.Detail)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Sources_is_done_with_no_action_when_every_named_source_is_connected()
    {
        var one = SetupSteps.Sources(InMemorySetupFacts.NamedSources(("slack", true)));
        var both = SetupSteps.Sources(InMemorySetupFacts.NamedSources(("github", true), ("slack", true)));

        Assert.Equal((SetupState.Done, null, "Every source your profile names is connected.", "Slack."), (one.State, one.Action, one.Why, one.Detail));
        Assert.Equal((SetupState.Done, null, "GitHub, Slack."), (both.State, both.Action, both.Detail));
        Assert.Equal(2, both.Items.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unreadable_sources_step_is_failed_with_a_plain_sentence_and_the_code_only_in_the_error()
    {
        var step = SetupSteps.Unreadable(SetupStepId.Sources, "SND-PROFILE-004");

        Assert.Equal((SetupState.Failed, "Connect your sources", "SND-PROFILE-004"), (step.State, step.Title, step.Error?.Code));
        Assert.DoesNotContain("SND-", step.Why, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_step_keeps_its_code_out_of_every_plain_text()
    {
        List<SetupStep> steps =
        [
            SetupSteps.Unreadable(SetupStepId.Sources, "SND-LEDGER-002"),
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
            SetupSteps.Repo(new RepoFact(0)), SetupSteps.Profile(new ProfileFact(ProfilePresence.Missing), new RepoFact(0)),
            SetupSteps.Mcp(new McpFact(0, 0, "Claude Code")), SetupSteps.Map(new MapFact(MapPhase.Waiting)), SetupSteps.Sources(new SourcesFact(0)),
        };

        Assert.Equal(Enum.GetValues<SetupStepId>(), all.Select(static s => s.Id));
        Assert.All(all, static s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Title));
            Assert.EndsWith(".", s.Why, StringComparison.Ordinal);
        });
    }
}
