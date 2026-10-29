using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SetupSources;

/// <summary>The setup list's real sources over in-memory ports: what each reads, and that none of them starts anything.</summary>
public sealed class SetupFactsTests
{
    private static CancellationToken Token => SetupFactsRig.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task Repos_count_what_the_live_profile_names_and_no_profile_is_none()
    {
        await using var rig = new SetupFactsRig();
        var none = (await rig.Facts.Repos(Token)).Value;
        rig.Profiles.Live = SetupFactsRig.ProfileWithRepos(2);

        var two = (await rig.Facts.Repos(Token)).Value;

        Assert.Equal((0, 2), (none.Count, two.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Profile_is_missing_unapproved_drifted_or_approved()
    {
        await using var rig = new SetupFactsRig();
        var missing = (await rig.Facts.Profile(Token)).Value.Presence;
        rig.Profiles.Live = SetupFactsRig.ProfileWithRepos();
        var unapproved = (await rig.Facts.Profile(Token)).Value.Presence;
        await rig.Approve(SetupFactsRig.ProfileWithRepos());
        var approved = (await rig.Facts.Profile(Token)).Value.Presence;
        rig.Profiles.Live = SetupFactsRig.ProfileWithRepos(2);
        var drifted = (await rig.Facts.Profile(Token)).Value.Presence;

        Assert.Equal([ProfilePresence.Missing, ProfilePresence.Unapproved, ProfilePresence.Approved, ProfilePresence.Drifted], [missing, unapproved, approved, drifted]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Profile_preview_says_what_the_live_profile_allows_and_is_absent_without_one()
    {
        await using var rig = new SetupFactsRig();
        var none = (await rig.Facts.Profile(Token)).Value.Preview;
        rig.Profiles.Live = SetupFactsRig.ProfileWithRepos();

        var preview = (await rig.Facts.Profile(Token)).Value.Preview;

        Assert.Null(none);
        Assert.Equal([new SetupPreviewLine("repo", "Works in demo (/work/demo)."), new SetupPreviewLine("allows", "Workflows may not change anything outside Sindri.")], preview);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_with_a_mistake_is_an_error_that_fails_its_own_step()
    {
        await using var rig = new SetupFactsRig();
        rig.Profiles.Live = SindriErrors.ProfileInvalid.With("services.heartbeat.every: not a duration");

        var profile = await rig.Facts.Profile(Token);
        var repos = await rig.Facts.Repos(Token);

        Assert.Equal((SindriErrors.ProfileInvalid.Code, SindriErrors.ProfileInvalid.Code), (profile.Error.Code, repos.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_counts_the_repos_whose_local_registration_exists()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos(2));
        var none = (await rig.Facts.Mcp(Token)).Value;
        rig.ClaudeHas((SetupFactsRig.Checkout, "demo"));

        var one = (await rig.Facts.Mcp(Token)).Value;
        rig.ClaudeHas((SetupFactsRig.Checkout, "demo"), ("/work/demo2", "demo2"));
        var both = (await rig.Facts.Mcp(Token)).Value;

        Assert.Equal([(2, 0), (2, 1), (2, 2)], [(none.Repos, none.Registered), (one.Repos, one.Registered), (both.Repos, both.Registered)]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_ignores_a_user_wide_entry_a_wrong_repo_a_damaged_file_and_a_file_it_cannot_read()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos());
        var claude = rig.Host.UserConfigPath(Core.Mcp.ProviderFile.Claude);

        rig.Host.Files[claude] = """{"mcpServers":{"sindri":{"url":"http://127.0.0.1:7480/sindri/mcp"}}}""";
        var userWide = (await rig.Facts.Mcp(Token)).Value.Registered;
        rig.ClaudeHas((SetupFactsRig.Checkout, "another-repo"));
        var otherRepo = (await rig.Facts.Mcp(Token)).Value.Registered;
        rig.Host.Files[claude] = "{ not json";
        var damaged = (await rig.Facts.Mcp(Token)).Value.Registered;
        _ = rig.Host.Files.Remove(claude);
        var absent = (await rig.Facts.Mcp(Token)).Value.Registered;

        Assert.Equal([0, 0, 0, 0], [userWide, otherRepo, damaged, absent]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_has_nothing_to_register_before_a_profile_with_a_repo_is_approved()
    {
        await using var rig = new SetupFactsRig();
        rig.Profiles.Live = SetupFactsRig.ProfileWithRepos();

        var fact = (await rig.Facts.Mcp(Token)).Value;

        Assert.Equal((0, 0), (fact.Repos, fact.Registered));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_with_no_provider_installed_names_none_and_registers_nothing()
    {
        await using var rig = new SetupFactsRig();
        _ = rig.Host.WithoutProvider(Core.Mcp.ProviderFile.Claude);
        await rig.Approve(SetupFactsRig.ProfileWithRepos());

        var fact = (await rig.Facts.Mcp(Token)).Value;

        Assert.Equal((1, 0, string.Empty), (fact.Repos, fact.Registered, fact.Providers));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_counts_a_repo_registered_only_when_every_installed_provider_has_it_and_lists_where()
    {
        var host = new FakeProviderHost();
        var withAll = new SetupFactsRig(host);
        await using (withAll)
        {
            await withAll.Approve(SetupFactsRig.ProfileWithRepos());
            withAll.ClaudeHas((SetupFactsRig.Checkout, "demo"));
            host.Files[SetupFactsRig.Checkout + "/.cursor/mcp.json"] = """{"mcpServers":{"sindri":{"url":"http://127.0.0.1:7480/sindri/mcp","headers":{"X-Sindri-Repo":"demo"}}}}""";
            var partial = (await withAll.Facts.Mcp(Token)).Value;
            host.Files[SetupFactsRig.Checkout + "/.codex/config.toml"] = "[mcp_servers.sindri]\nurl = \"http://127.0.0.1:7480/sindri/mcp\"\nhttp_headers = { \"X-Sindri-Repo\" = \"demo\" }\n";
            var all = (await withAll.Facts.Mcp(Token)).Value;

            Assert.Equal(("Claude Code, Codex, Cursor", 0, "Claude Code, Cursor"), (partial.Providers, partial.Registered, partial.Items.Lines[0].Detail));
            Assert.Equal((1, "Claude Code, Codex, Cursor", SetupState.Done), (all.Registered, all.Items.Lines[0].Detail, all.Items.Lines[0].State));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_item_lists_each_installed_provider_as_connected_or_not_started()
    {
        var host = new FakeProviderHost();
        await using var rig = new SetupFactsRig(host);
        await rig.Approve(SetupFactsRig.ProfileWithRepos());
        rig.ClaudeHas((SetupFactsRig.Checkout, "demo"));

        var item = (await rig.Facts.Mcp(Token)).Value.Items.Lines[0];

        Assert.Equal(
            [new McpProviderState("claude", McpConnection.Connected), new McpProviderState("codex", McpConnection.NotStarted), new McpProviderState("cursor", McpConnection.NotStarted)],
            item.Providers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_item_lists_only_the_installed_providers()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos());

        var item = (await rig.Facts.Mcp(Token)).Value.Items.Lines[0];

        Assert.Equal([new McpProviderState("claude", McpConnection.NotStarted)], item.Providers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_item_is_connecting_while_a_register_job_for_the_repo_runs()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos());
        rig.RegisterJob("demo", JobState.Running);

        var providers = (await rig.Facts.Mcp(Token)).Value.Items.Lines[0].Providers;

        Assert.Equal([new McpProviderState("claude", McpConnection.Connecting)], providers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_item_is_connecting_while_a_register_job_for_the_repo_waits_in_the_queue()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos());
        _ = await rig.Queue.Submit(JobKind.From("RegisterMcp"), McpJobKeys.For(RepoName.From("demo")), static _ => ValueTask.FromResult<Result<Unit>>(SindriErrors.JobsRateLimited.With("full")), Token);

        var providers = (await rig.Facts.Mcp(Token)).Value.Items.Lines[0].Providers;

        Assert.Equal([new McpProviderState("claude", McpConnection.Connecting)], providers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mcp_item_is_failed_when_the_newest_register_job_failed_and_not_started_after_a_cancel_or_a_success()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos());
        rig.RegisterJob("demo", JobState.Failed, "Couldn't register with Claude Code");
        rig.RegisterJob("demo", JobState.Cancelled);
        var failed = (await rig.Facts.Mcp(Token)).Value.Items.Lines[0].Providers;
        rig.RegisterJob("demo", JobState.Succeeded);
        var succeededButUnregistered = (await rig.Facts.Mcp(Token)).Value.Items.Lines[0].Providers;

        Assert.Equal([new McpProviderState("claude", McpConnection.Failed, "Couldn't register with Claude Code")], failed);
        Assert.Equal([new McpProviderState("claude", McpConnection.NotStarted)], succeededButUnregistered);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Repos_and_the_map_carry_one_line_per_repo_of_the_profile()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos(2));

        var repos = (await rig.Facts.Repos(Token)).Value;
        var map = (await rig.Facts.Map(Token)).Value;

        Assert.Equal(["demo", "demo2"], repos.Items.Lines.Select(static i => i.Repo), StringComparer.Ordinal);
        Assert.Equal(["demo", "demo2"], map.Items.Lines.Select(static i => i.Repo), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_waits_for_an_approved_profile_with_a_repo_and_never_asks_the_engine_before()
    {
        await using var rig = new SetupFactsRig();
        var noProfile = (await rig.Facts.Map(Token)).Value.Phase;

        Assert.Equal(MapPhase.Waiting, noProfile);
        Assert.Equal(0, rig.Engine.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_is_built_when_the_engine_holds_a_current_map()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos());
        rig.Engine.Answer = SetupFactsRig.CurrentMap();

        Assert.Equal(MapPhase.Built, (await rig.Facts.Map(Token)).Value.Phase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_without_a_map_follows_the_build_jobs()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos());
        var notBuilt = (await rig.Facts.Map(Token)).Value;
        rig.BuildJob(JobState.Failed, ["extracting 3/100 files"], "SND-CONTEXT-040");
        var failed = (await rig.Facts.Map(Token)).Value;
        rig.BuildJob(JobState.Running, ["starting", "extracting 40/100 files"]);
        var running = (await rig.Facts.Map(Token)).Value;

        Assert.Equal(MapPhase.NotBuilt, notBuilt.Phase);
        Assert.Equal((MapPhase.Failed, "SND-CONTEXT-040", null), (failed.Phase, failed.FailureCode, failed.Progress));
        Assert.Equal((MapPhase.Building, new SetupProgress(40, 100, "files")), (running.Phase, running.Progress));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_passes_on_an_engine_error_that_is_not_the_missing_map()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos());
        rig.Engine.Answer = ContextErrors.SettingsInvalid.With("context.deadline: not a duration");

        Assert.Equal(ContextErrors.SettingsInvalid.Code, (await rig.Facts.Map(Token)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Recipes_count_each_recipe_once_and_approval_moves_it_to_approved()
    {
        await using var rig = new SetupFactsRig();
        _ = await rig.Registry.H.HoldLease();
        var empty = (await rig.Facts.Recipes(Token)).Value;
        var step = await rig.Registry.Shipped("sindri/workflow.step.yaml");
        var registered = (await rig.Facts.Recipes(Token)).Value;
        _ = (await rig.Registry.Approve(step)).Value;

        var approved = (await rig.Facts.Recipes(Token)).Value;

        Assert.Equal([(0, 0), (1, 0), (1, 1)], [(empty.Total, empty.Approved), (registered.Total, registered.Approved), (approved.Total, approved.Approved)]);
    }

    private const string BothSources = """{"sources":{"github":{"repos":["owner/name"],"token":"env:GH_KEY"},"slack":{"channels":[{"id":"C0001GEN","name":"eng"}],"token":"env:SLACK_KEY"}}}""";

    [Fact, Trait("Lane", "unit")]
    public async Task Sources_name_none_without_an_approved_profile_and_none_for_a_profile_that_names_only_linear()
    {
        await using var rig = new SetupFactsRig();
        var missing = (await rig.Facts.Sources(Token)).Value;
        await rig.Approve(SetupFactsRig.ProfileWithContext("""{"sources":{"linear":{"token":"env:LINEAR_KEY"}}}"""));
        rig.Env["LINEAR_KEY"] = "lin_live-secret";

        var linearOnly = (await rig.Facts.Sources(Token)).Value;

        Assert.Equal([(0, 0), (0, 0)], [(missing.Count, missing.Items.Lines.Count), (linearOnly.Count, linearOnly.Items.Lines.Count)]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_is_connected_when_its_token_pointer_resolves_and_needs_a_token_when_it_does_not()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithContext(BothSources));
        rig.Env["GH_KEY"] = "ghp_live-secret-0123";

        var fact = (await rig.Facts.Sources(Token)).Value;

        Assert.Equal(2, fact.Count);
        Assert.Equal([("github", SetupState.Done, "connected"), ("slack", SetupState.Todo, "needs token")], fact.Items.Lines.Select(static i => (i.Repo, i.State, i.Detail)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_whose_profile_entry_has_no_token_pointer_needs_a_token_and_a_secret_is_never_in_the_fact()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithContext("""{"sources":{"github":{"repos":["owner/name"]},"slack":{"channels":[{"id":"C0001GEN","name":"eng"}],"token":"env:SLACK_KEY"}}}"""));
        rig.Env["SLACK_KEY"] = "xoxb-live-secret-0123";

        var fact = (await rig.Facts.Sources(Token)).Value;

        Assert.Equal([("github", SetupState.Todo, "needs token"), ("slack", SetupState.Done, "connected")], fact.Items.Lines.Select(static i => (i.Repo, i.State, i.Detail)));
        Assert.DoesNotContain("xoxb-live-secret-0123", string.Join('|', fact.Items.Lines.Select(static i => i.Detail)), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Sources_read_from_the_approved_profile_not_the_live_one()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithContext("""{"sources":{"github":{"repos":["owner/name"]}}}"""));
        rig.Profiles.Live = SetupFactsRig.ProfileWithContext(BothSources);

        var fact = (await rig.Facts.Sources(Token)).Value;

        Assert.Equal(["github"], fact.Items.Lines.Select(static i => i.Repo), StringComparer.Ordinal);
    }
}
