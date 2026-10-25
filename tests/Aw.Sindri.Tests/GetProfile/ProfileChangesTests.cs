using System.Text.Json;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.GetProfile;

namespace Aw.Sindri.Tests.GetProfile;

/// <summary>What the live profile asks for beyond the approved one, in words (setup-once spec §5.2 "Preview").</summary>
public sealed class ProfileChangesTests
{
    private static readonly ShippedBlockSentences _sentences = new();

    [Fact, Trait("Lane", "unit")]
    public void With_nothing_approved_every_repo_service_and_effect_is_new()
    {
        var live = Profile(repos: [new RepoEntry("web", "/work/web")], services: new(StringComparer.Ordinal) { ["heartbeat"] = new ServiceSettings("5s") }, effects: ["effect.claim.ref"]);

        var changes = ProfileChanges.Between(approved: null, live, _sentences);

        Assert.Equal(["web"], changes.NewRepos.Select(static r => r.Name), StringComparer.Ordinal);
        Assert.Equal(["Sindri will run the heartbeat service every 5 seconds"], changes.Services);
        Assert.Equal(["Reserves a work item so no other run starts on it"], changes.Allowance);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_the_approved_profile_names_is_not_new()
    {
        var approved = Profile(repos: [new RepoEntry("web", "/work/web")]);
        var live = Profile(repos: [new RepoEntry("web", "/work/web"), new RepoEntry("api", "/work/api")]);

        var changes = ProfileChanges.Between(approved, live, _sentences);

        Assert.Equal(["api"], changes.NewRepos.Select(static r => r.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_service_run_more_often_is_said_with_both_cadences_and_a_slower_one_is_not_said()
    {
        var approved = Profile(services: new(StringComparer.Ordinal) { ["heartbeat"] = new ServiceSettings("1h"), ["observe"] = new ServiceSettings("5m") });
        var live = Profile(services: new(StringComparer.Ordinal) { ["heartbeat"] = new ServiceSettings("1m"), ["observe"] = new ServiceSettings("1h") });

        var changes = ProfileChanges.Between(approved, live, _sentences);

        Assert.Equal(["Sindri will run the heartbeat service more often: every 1 minute instead of every 1 hour"], changes.Services);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_effect_the_approved_profile_allows_is_not_new_and_an_unknown_kind_is_said_by_its_name()
    {
        var approved = Profile(effects: ["effect.claim.ref"]);
        var live = Profile(effects: ["effect.claim.ref", "effect.notify.desk", "effect.deploy.canary-ring"]);

        var changes = ProfileChanges.Between(approved, live, _sentences);

        Assert.Equal(["Puts a question or notice in your Needs you list", "Allows the effect \"deploy canary ring\""], changes.Allowance);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_that_asks_for_nothing_new_is_empty()
    {
        var same = Profile(repos: [new RepoEntry("web", "/work/web")], effects: ["effect.claim.ref"]);

        Assert.True(ProfileChanges.Between(same, same, _sentences).IsEmpty);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_has_three_sentences_in_reading_order()
    {
        Assert.Equal(
            ["Sindri will read /work/web and send what it reads to your Onyx and to the map cleanup agent",
             "Sindri will write its MCP settings into web for Claude Code, Codex and Cursor",
             "Sindri will keep web's map up to date on its own"],
            ProfileChanges.RepoSentences("web", "/work/web"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Allowance_says_a_budget_or_limit_the_live_profile_adds_or_raises_and_nothing_for_one_that_stays_or_drops()
    {
        var approved = Profile(agents: Agents(("claude", 5m), ("codex", 8m)), line: new LineSettings(Parallel: 2, MaxOpenPrs: 6), pr: new PrSettings(AutofixCap: 3));
        var live = Profile(agents: Agents(("claude", 10m), ("codex", 8m), ("cursor", 3m)), line: new LineSettings(Parallel: 4, MaxOpenPrs: 4, MaxStack: 6), pr: new PrSettings(AutofixCap: 5));

        var changes = ProfileChanges.Between(approved, live, _sentences);

        Assert.Equal(
            ["claude may spend up to $10 a day", "cursor may spend up to $3 a day", "Sindri may run up to 4 phases at once", "Sindri may stack phases up to 6 deep", "Sindri may push up to 5 CI fixes for one pull request"],
            changes.Allowance);
    }

    [Fact, Trait("Lane", "unit")]
    public void Allowance_says_a_removed_daily_budget_and_a_raised_size_budget()
    {
        var approved = Profile(agents: Agents(("claude", 5m)), plan: new PlanSettings(Sizes: new Dictionary<string, SizeSettings>(StringComparer.Ordinal) { ["S"] = new SizeSettings(Budget: new BudgetSettings(Tokens: 1000, Turns: 10)) }));
        var live = Profile(agents: Agents(("claude", null)), plan: new PlanSettings(Sizes: new Dictionary<string, SizeSettings>(StringComparer.Ordinal) { ["S"] = new SizeSettings(Budget: new BudgetSettings(Tokens: 2000, Turns: 10, WallMinutes: 30)) }));

        var changes = ProfileChanges.Between(approved, live, _sentences);

        Assert.Equal(["claude will have no daily spending limit", "A S phase may use up to 2000 tokens, 30 minutes"], changes.Allowance);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_nothing_approved_every_limit_the_profile_sets_is_said()
    {
        var live = Profile(agents: Agents(("claude", 5m)), line: new LineSettings(MaxStack: 2));

        Assert.Equal(["claude may spend up to $5 a day", "Sindri may stack phases up to 2 deep"], ProfileChanges.Between(approved: null, live, _sentences).Allowance);
    }

    [Fact, Trait("Lane", "unit")]
    public void Connections_say_the_forge_onyx_judge_providers_and_agents_the_live_profile_adds()
    {
        var context = JsonDocument.Parse("""{"sources":{"onyx":{"url":"http://127.0.0.1:3189","token":"keychain:onyx/x","managed":true}}}""").RootElement;
        var live = new SindriProfile(
            1,
            "t",
            Workflow: new WorkflowSettings(
                Judge: new JudgeSettings(["claude-cli", "jev"]),
                Agent: new WorkflowAgentSettings(AgentId.From("claude")),
                Forge: new ForgeSettings("github", ["github.com"], "env:GH_TOKEN")),
            Context: context,
            Setup: new SetupSettings(SetupSettings.Auto));

        var changes = ProfileChanges.Between(approved: null, live, _sentences);

        Assert.Equal(
            ["Sindri will send what it reads to your Onyx at http://127.0.0.1:3189", "Sindri will start and look after Onyx on this machine", "Sindri will use the github forge at github.com, with the token in GH_TOKEN", "Judge steps may ask claude-cli", "Judge steps may ask jev", "Workflow steps will launch the claude agent", "Sindri will register its MCP server in each repo on its own"],
            changes.Connections);
        Assert.DoesNotContain(changes.Connections, static c => c.Contains("keychain", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Connections_leave_out_what_the_approved_profile_already_has()
    {
        var live = new SindriProfile(1, "t", Workflow: new WorkflowSettings(Judge: new JudgeSettings(["claude-cli", "jev"]), Forge: new ForgeSettings("github", ["github.com"])));
        var approved = new SindriProfile(1, "t", Workflow: new WorkflowSettings(Judge: new JudgeSettings(["claude-cli"]), Forge: new ForgeSettings("github", ["github.com", "ghe.example"])));

        var changes = ProfileChanges.Between(approved, live, _sentences);

        Assert.Equal(["Judge steps may ask jev"], changes.Connections);
    }

    [Fact, Trait("Lane", "unit")]
    public void Connections_never_name_the_rules_provider_since_it_asks_nothing_outside()
    {
        var live = new SindriProfile(1, "t", Workflow: new WorkflowSettings(Judge: new JudgeSettings(["claude-cli", JudgeProviderNames.Rules])));

        Assert.Equal(["Judge steps may ask claude-cli"], ProfileChanges.Between(approved: null, live, _sentences).Connections);
    }

    [Fact, Trait("Lane", "unit")]
    public void Human_only_says_the_steps_the_profile_keeps_for_the_owner_and_not_ones_already_approved()
    {
        var live = new SindriProfile(1, "t", Workflow: new WorkflowSettings(Pr: new PrSettings(Replies: "desk", HeldBlocksMerge: true), Route: new RouteSettings(RouteSettings.None)));

        var fresh = ProfileChanges.Between(approved: null, live, _sentences);
        var same = ProfileChanges.Between(live, live, _sentences);

        Assert.Equal(["You send every reply to a pull request comment yourself", "A held comment stops the merge until you clear it", "Routed items stop at the router until you start them"], fresh.HumanOnly);
        Assert.Empty(same.HumanOnly);
        Assert.True(same.IsEmpty);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_with_no_such_settings_has_empty_groups()
    {
        var changes = ProfileChanges.Between(approved: null, Profile(repos: [new RepoEntry("web", "/work/web")]), _sentences);

        Assert.Empty(changes.Allowance);
        Assert.Empty(changes.Connections);
        Assert.Empty(changes.HumanOnly);
    }

    private static AgentSettings Agents(params (string Id, decimal? Budget)[] targets) =>
        new(Targets: targets.ToDictionary(static t => t.Id, static t => new AgentLimits(t.Budget), StringComparer.Ordinal));

    private static SindriProfile Profile(IReadOnlyList<RepoEntry>? repos = null, Dictionary<string, ServiceSettings>? services = null, IReadOnlyList<string>? effects = null, AgentSettings? agents = null, LineSettings? line = null, PrSettings? pr = null, PlanSettings? plan = null) =>
        new(1, "t", Repos: repos, Services: services, Workflow: effects is null && line is null && pr is null && plan is null ? null : new WorkflowSettings(Effects: effects, Line: line, Pr: pr, Plan: plan), Agents: agents);
}
