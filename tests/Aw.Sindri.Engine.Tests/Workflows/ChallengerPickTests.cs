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

/// <summary>The second opinion runs on an agent other than the drafter's, or the Step parks with a named reason (#1008).</summary>
[Lane(TestLane.Unit)]
public sealed class ChallengerPickTests
{
    private static readonly InstanceAddress _instance = InstanceAddress.From(A.FirstItemId.Value + "/scoping/1");
    private static readonly LaunchId _draft = LaunchId.From("LCH-" + new string('a', 32));
    private static readonly LaunchId _challenge = LaunchId.From("LCH-" + new string('b', 32));

    [Fact]
    public async Task A_challenger_differs_from_the_drafter_when_the_catalog_has_a_second_agent()
    {
        var pick = await Pick([AgentEntries.ClaudeCode, AgentEntries.Codex], drafted: "claude-code");

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("codex", picked.Value.Value);
    }

    [Fact]
    public async Task A_challenger_is_not_picked_from_an_entry_a_Step_may_not_use()
    {
        // aider is an added entry with no approved argv hash, so an unattended Step can't use it
        var pick = await Pick([AgentEntries.ClaudeCode, AgentEntries.Aider], drafted: "claude-code");

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-LAUNCH-028", picked.Error.Code);
    }

    [Fact]
    public async Task With_no_second_usable_agent_the_pick_names_the_one_there_is()
    {
        var pick = await Pick([AgentEntries.ClaudeCode], drafted: "claude-code");

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-LAUNCH-028", picked.Error.Code);
        Assert.Contains("claude-code", picked.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_launch_already_recorded_keeps_its_agent_when_the_catalog_has_changed_since()
    {
        // the first attempt ran the challenger on cursor; a retry must not re-pick (codex would now be first)
        var pick = await Pick([AgentEntries.ClaudeCode, AgentEntries.Codex, AgentEntries.Cursor], drafted: "claude-code", challenged: "cursor");

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("cursor", picked.Value.Value);
    }

    [Fact]
    public async Task With_no_drafter_launch_the_profile_default_stands()
    {
        var pick = await Pick([AgentEntries.ClaudeCode, AgentEntries.Codex], drafted: null);

        var picked = await pick.Pick(Ask(), TestContext.Current.CancellationToken);

        Assert.Equal("claude-code", picked.Value.Value);
    }

    private static ChallengerAsk Ask() => new(A.FirstItemId, _instance, _challenge, "draft", AgentId.From("claude-code"));

    private static async Task<ChallengerPick> Pick(AgentEntry[] entries, string? drafted, string? challenged = null)
    {
        var rows = new List<LaunchRow>();
        if (challenged is not null)
        {
            rows.Add(Row(_challenge, challenged, "challenge"));
        }

        if (drafted is not null)
        {
            rows.Add(Row(_draft, drafted, "draft"));
        }

        var profile = ProfileRules.Check(new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester")).Value;
        var profiles = new InMemoryProfiles();
        _ = await profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), TestContext.Current.CancellationToken);
        return new ChallengerPick(new FixedAgentCatalog(entries), new Launches(rows), profiles);
    }

    private static LaunchRow Row(LaunchId id, string agent, string state) =>
        new(id, A.FirstItemId, Repo: null, LaunchMode.Open, AgentId.From(agent), "engine", LaunchState.Started, DateTimeOffset.UnixEpoch, OutcomeAt: null,
            Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null, Ended: null,
            new LaunchStep(_instance, state, Attempt: 1), Sequence: 1);

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
