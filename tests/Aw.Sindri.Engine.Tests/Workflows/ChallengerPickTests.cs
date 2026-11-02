using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The second opinion runs on an agent other than the drafter's and carries the drafter's newest launch to the model pick (#1008).</summary>
[Lane(TestLane.Unit)]
public sealed class ChallengerPickTests
{
    private static readonly InstanceAddress _instance = InstanceAddress.From(A.FirstItemId.Value + "/scoping/1");
    private static readonly LaunchId _draft = LaunchId.From("LCH-" + new string('a', 32));
    private static readonly LaunchId _challenge = LaunchId.From("LCH-" + new string('b', 32));

    [Fact]
    public async Task A_challenger_differs_from_the_drafter_when_the_catalog_has_a_second_agent()
    {
        var pick = await Pick([AgentEntries.ClaudeCode, AgentEntries.Codex], drafted: ("claude-code", "opus"));

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("codex", picked.Value.Agent.Value);
        Assert.Equal(new DrafterModel(SameAgent: false, Model: "opus"), picked.Value.Drafter);
    }

    [Fact]
    public async Task A_challenger_is_not_picked_from_an_entry_a_Step_may_not_use()
    {
        // aider is an added entry with no approved argv hash, so an unattended Step can't use it; codex is next
        var pick = await Pick([AgentEntries.ClaudeCode, AgentEntries.Aider, AgentEntries.Codex], drafted: ("claude-code", "opus"));

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("codex", picked.Value.Agent.Value);
    }

    [Fact]
    public async Task The_drafter_is_its_newest_launch_when_it_ran_more_than_once()
    {
        // a redraft after a found challenge: the second draft ran on sonnet, the first on opus
        var pick = await Pick([AgentEntries.ClaudeCode, AgentEntries.Codex], drafted: ("claude-code", "sonnet"), earlierDraft: ("claude-code", "opus"));

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("sonnet", picked.Value.Drafter?.Model);
    }

    [Fact]
    public async Task A_launch_already_recorded_keeps_its_agent_when_the_catalog_has_changed_since()
    {
        // the first attempt ran the challenger on cursor; a retry must not re-pick (codex would now be first)
        var pick = await Pick([AgentEntries.ClaudeCode, AgentEntries.Codex, AgentEntries.Cursor], drafted: ("claude-code", "opus"), challenged: "cursor");

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("cursor", picked.Value.Agent.Value);
    }

    [Fact]
    public async Task With_no_drafter_launch_the_profile_default_stands_and_there_is_no_drafter_to_differ_from()
    {
        var pick = await Pick([AgentEntries.ClaudeCode, AgentEntries.Codex], drafted: null);

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("claude-code", picked.Value.Agent.Value);
        Assert.Null(picked.Value.Drafter);
    }

    [Fact]
    public async Task With_no_agent_the_Step_may_use_the_pick_parks_with_SND_LAUNCH_028()
    {
        // aider is an added entry with no approved argv hash: nothing in the catalog may run an unattended Step
        var pick = await Pick([AgentEntries.Aider], drafted: ("claude-code", "opus"));

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-LAUNCH-028", picked.Error.Code);
    }

    [Fact]
    public async Task One_usable_agent_is_not_a_park_it_stays_on_the_drafters_agent()
    {
        var pick = await Pick([AgentEntries.ClaudeCode], drafted: ("claude-code", "opus"));

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.True(picked.IsSuccess);
        Assert.Equal("claude-code", picked.Value.Agent.Value);
        Assert.Equal(new DrafterModel(SameAgent: true, Model: "opus"), picked.Value.Drafter);
    }

    private static ChallengerAsk Ask() => new(A.FirstItemId, _instance, _challenge, "draft", AgentId.From("claude-code"));

    private static async Task<ChallengerPick> Pick(AgentEntry[] entries, (string Agent, string Model)? drafted, string? challenged = null, (string Agent, string Model)? earlierDraft = null)
    {
        var rows = new List<LaunchRow>();
        if (challenged is not null)
        {
            rows.Add(Row(_challenge, challenged, model: null, "challenge"));
        }

        if (drafted is { } latest)
        {
            rows.Add(Row(_draft, latest.Agent, latest.Model, "draft"));   // newest first, as the view serves them
        }

        if (earlierDraft is { } earlier)
        {
            rows.Add(Row(LaunchId.From("LCH-" + new string('c', 32)), earlier.Agent, earlier.Model, "draft"));
        }

        var profile = ProfileRules.Check(new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester")).Value;
        var profiles = new InMemoryProfiles();
        _ = await profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), TestContext.Current.CancellationToken);
        return new ChallengerPick(new FixedAgentCatalog(entries), new Launches(rows), profiles);
    }

    private static LaunchRow Row(LaunchId id, string agent, string? model, string state)
    {
        var row = new LaunchRow(id, A.FirstItemId, Repo: null, LaunchMode.Open, AgentId.From(agent), "engine", LaunchState.Started, DateTimeOffset.UnixEpoch, OutcomeAt: null,
            Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null, Ended: null,
            new LaunchStep(_instance, state, Attempt: 1), Sequence: 1);
        return row with { Model = model };
    }

    /// <summary>The item's launches, newest first, as the view serves them.</summary>
    private sealed class Launches(IReadOnlyList<LaunchRow> rows) : ILaunchViews
    {
        public ValueTask<Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(rows.FirstOrDefault(r => r.Id == launch)));

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(rows));

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LaunchRow>>> Since(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
