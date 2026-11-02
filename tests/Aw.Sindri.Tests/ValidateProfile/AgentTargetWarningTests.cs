using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.ValidateProfile;

namespace Aw.Sindri.Tests.ValidateProfile;

/// <summary>Issue #1428: a target that can't take a Step's policy is said at validate and in the doctor, not only as a parked instance.</summary>
public sealed class AgentTargetWarningTests
{
    private static readonly AgentEntry _cursor = Entry("cursor", PolicyForm.None);
    private static readonly AgentEntry _reserved = Entry("reserved", PolicyForm.CursorConfig);
    private static readonly AgentEntry _claude = Entry("claude-code", PolicyForm.ClaudeSettingsFile);
    private static readonly Catalog _catalog = new(_cursor, _reserved, _claude);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory, Trait("Lane", "unit")]
    [InlineData("cursor")]
    [InlineData("reserved")]
    public void A_target_whose_policy_form_cant_be_used_is_warned_with_SND_LAUNCH_013(string target)
    {
        var line = AgentTargetWarning.For(AgentId.From(target), _catalog);

        Assert.NotNull(line);
        Assert.Contains($"workflow.agent.target '{target}'", line, StringComparison.Ordinal);
        Assert.Contains("SND-LAUNCH-013", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_target_with_a_policy_form_is_not_warned() => Assert.Null(AgentTargetWarning.For(AgentId.From("claude-code"), _catalog));

    [Fact, Trait("Lane", "unit")]
    public void A_target_not_in_this_boxs_catalog_is_not_warned() => Assert.Null(AgentTargetWarning.For(AgentId.From("aider"), _catalog));

    [Fact, Trait("Lane", "unit")]
    public void No_target_is_not_warned() => Assert.Null(AgentTargetWarning.For(target: null, _catalog));

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_lists_the_warning_and_still_succeeds()
    {
        var h = new SliceHarness();
        h.Profiles.Live = Loaded("cursor");
        var service = new ValidateProfileService(h.Profiles, h.Profiles, h.Store, h.Events, FakeContextEngine.Instance, agents: _catalog);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.True(validated.IsSuccess);
        var warning = Assert.Single(validated.Value.Warnings!);
        Assert.Contains("SND-LAUNCH-013", warning, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_has_no_warnings_for_a_target_that_can_take_a_policy()
    {
        var h = new SliceHarness();
        h.Profiles.Live = Loaded("claude-code");
        var service = new ValidateProfileService(h.Profiles, h.Profiles, h.Store, h.Events, FakeContextEngine.Instance, agents: _catalog);

        Assert.Null((await service.Handle(new ValidateProfileRequest(), Token)).Value.Warnings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_doctor_adds_an_agent_target_warn_row_with_a_fix()
    {
        var checks = await new AgentTargetDoctor(new Approved(Loaded("cursor")), _catalog).Run(Token);

        var row = Assert.Single(checks);
        Assert.Equal("agent:target", row.Name);
        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("SND-LAUNCH-013", row.Detail, StringComparison.Ordinal);
        Assert.NotNull(row.Fix);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("claude-code")]
    [InlineData("aider")]
    public async Task The_doctor_adds_no_row_for_a_good_or_unknown_target(string target) =>
        Assert.Empty(await new AgentTargetDoctor(new Approved(Loaded(target)), _catalog).Run(Token));

    [Fact, Trait("Lane", "unit")]
    public async Task The_doctor_adds_no_row_when_nothing_is_approved() =>
        Assert.Empty(await new AgentTargetDoctor(new Approved(profile: null), _catalog).Run(Token));

    /// <summary>Pins that the policy refusal stays until a verified Cursor form exists (the repo can't confirm the CLI's permission format).</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_Step_policy_on_the_cursor_builtin_shape_still_parks_SND_LAUNCH_013()
    {
        var step = new StepArgs(Model: null, Policy: new StepPolicy([], StepPermissionMode.ReadOnly, "/tmp/step"), new StepRef("draft", "agent"));

        var result = StepForms.Arguments(_cursor, step, policyFilePath: null);

        Assert.Equal("SND-LAUNCH-013", result.Error.Code);
    }

    private static AgentEntry Entry(string id, PolicyForm policy) => new(AgentId.From(id), id, id, ProcessName: null, Transcripts: null, Resume: null) { PolicyForm = policy };

    private static SindriProfile WithTarget(string target) =>
        new(1, "thor", Workflow: new WorkflowSettings(Agent: new WorkflowAgentSettings(Target: AgentId.From(target))));

    private static LoadedProfile Loaded(string target) => new(WithTarget(target), ProfileHash.From(new string('a', 64)), "/home/profile.yaml");

    private sealed class Catalog(params AgentEntry[] entries) : IAgentCatalog
    {
        public IReadOnlyList<AgentEntry> All => entries;

        public AgentEntry? Resolve(AgentId id) => entries.FirstOrDefault(e => e.Id == id);
    }

    private sealed class Approved(LoadedProfile? profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(profile);
    }
}
