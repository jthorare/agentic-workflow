using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The approved profile's 'workflow.agent' block as the model pick reads it (model-pick M-5): on only when the block says 'on', the pin by state before agent, the cap by agent, the profile's model list.</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileModelPickSettingsTests
{
    private static readonly AgentId _claude = AgentId.From("claude-code");

    private static ProfileModelPickSettings Settings(WorkflowAgentSettings? agent, IReadOnlyList<string>? deny = null, bool approvedProfile = true) =>
        new(new Approved(approvedProfile ? Loaded(agent, deny) : null));

    private static LoadedProfile Loaded(WorkflowAgentSettings? agent, IReadOnlyList<string>? deny) =>
        new(new SindriProfile(1, "thor", Workflow: new WorkflowSettings(Agent: agent), Index: deny is null ? null : new IndexSettings(DenyPaths: deny)), ProfileHash.From("3f1c2a" + new string('0', 58)), "profile.yaml");

    private static WorkflowAgentSettings On() => new(Pick: WorkflowAgentSettings.On);

    [Fact]
    public async Task No_approved_profile_picks_nothing()
    {
        var settings = Settings(On(), approvedProfile: false);

        Expect.Equal((false, ModelPickSettings.Off), (await settings.PickEnabled(Token), await settings.For(_claude, "draft", Token)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("off")]
    public async Task A_profile_with_pick_absent_or_off_is_off_for_every_agent(string? pick)
    {
        var settings = Settings(new WorkflowAgentSettings(Pick: pick, Pin: new Dictionary<string, string>(StringComparer.Ordinal) { ["claude-code"] = "haiku" }));

        Expect.Equal((false, ModelPickSettings.Off), (await settings.PickEnabled(Token), await settings.For(_claude, "draft", Token)));
    }

    [Fact]
    public async Task A_state_pin_beats_the_agent_pin_and_the_cap_is_the_agents_own()
    {
        var agent = On() with
        {
            Pin = new Dictionary<string, string>(StringComparer.Ordinal) { ["claude-code"] = "haiku", ["claude-code/draft"] = "opus" },
            Cap = new Dictionary<string, string>(StringComparer.Ordinal) { ["claude-code"] = "sonnet", ["codex"] = "gpt-5.4-mini" },
        };
        var settings = Settings(agent);

        var draft = await settings.For(_claude, "draft", Token);
        var check = await settings.For(_claude, "check", Token);

        Expect.Equal((true, "opus", "sonnet"), (await settings.PickEnabled(Token), draft.Pin, draft.Cap));
        Expect.Equal(("haiku", "sonnet"), (check.Pin, check.Cap));
    }

    [Fact]
    public async Task The_profiles_model_list_keeps_its_order_and_default_and_the_deny_globs_come_from_the_index()
    {
        var agent = On() with { Models = new Dictionary<string, AgentModels>(StringComparer.Ordinal) { ["claude-code"] = new(["haiku", "sonnet", "opus"], "sonnet") } };
        var settings = Settings(agent, deny: ["secrets/**"]);

        var read = await settings.For(_claude, "draft", Token);

        Expect.Sequence([("haiku", 0), ("sonnet", 1), ("opus", 2)], read.Models!.Select(static m => (m.Slug, m.Order!.Value)));
        Expect.Equal(("sonnet", ModelSource.Profile), (read.Default, read.Models![0].Source));
        Expect.Sequence(["secrets/**"], read.DenyPaths!);
    }

    [Fact]
    public async Task An_agent_with_no_entry_gets_no_list_pin_or_cap()
    {
        var agent = On() with { Models = new Dictionary<string, AgentModels>(StringComparer.Ordinal) { ["codex"] = new(["gpt-5.4-mini"], "gpt-5.4-mini") } };

        var read = await Settings(agent).For(_claude, "draft", Token);

        Expect.Equal((true, null, null, null, null), (read.PickOn, read.Pin, read.Cap, read.Models, read.Default));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Approved(LoadedProfile? profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(profile);
    }
}
