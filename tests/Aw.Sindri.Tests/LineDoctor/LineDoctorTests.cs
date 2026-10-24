using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.LineDoctor;
using Aw.Sindri.Host.Commands.Line;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LineDoctor;

/// <summary>'line doctor': the setup checks over the approved profile, the checkout and the box's cap now.</summary>
public sealed class LineDoctorTests
{
    private readonly InMemoryProfiles _profiles = new();
    private readonly FakeRepoLocator _locator = new FakeRepoLocator().In("/work/demo", "/work/demo").In("/work/other", "/work/other");
    private readonly FakeBoxLimits _box = new();
    private readonly FakeLineSetupProbe _probe = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorkflowSettings Ready() => new(
        Build: new BuildSettings(["dotnet build", "dotnet test"]),
        Line: new LineSettings(Parallel: 2),
        Forge: new ForgeSettings(Adapter: "github"));

    private async Task Approve(WorkflowSettings workflow)
    {
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("demo", "/work/demo")], Workflow: workflow);
        _ = await _profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Token);
    }

    private async Task<LineDoctorResponse> Doctor(string directory = "/work/demo") =>
        (await new LineDoctorService(_profiles, _locator, _box, _probe).Handle(new LineDoctorRequest(directory), Token)).Value;

    private static DoctorCheck Row(LineDoctorResponse report, string name) => report.Checks.Single(c => c.Name == name);

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_passes_a_repo_that_is_set_up()
    {
        await Approve(Ready());

        var report = await Doctor();

        Assert.True(report.Passed, string.Join('\n', report.Checks.Where(static c => c.State != CheckState.Ok).Select(static c => $"{c.Name}: {c.Detail}")));
        Assert.Equal("demo", report.Repo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_fails_when_the_profile_lists_no_build_command()
    {
        await Approve(Ready() with { Build = null });

        var row = Row(await Doctor(), "workflow.build.commands");

        Assert.Equal(CheckState.Fail, row.State);
        Assert.Contains("workflow.build.commands", row.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_fails_when_the_heavy_slot_cap_dropped_below_the_approved_parallel()
    {
        await Approve(Ready() with { Line = new LineSettings(Parallel: 3) });
        Assert.Equal(CheckState.Ok, Row(await Doctor(), "workflow.line.parallel").State);

        _box.HeavySlots = 2;   // the cap dropped after the profile was approved

        var row = Row(await Doctor(), "workflow.line.parallel");
        Assert.Equal(CheckState.Fail, row.State);
        Assert.Contains("heavy-slot cap of 2", row.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_does_not_hold_a_parallel_left_out_to_the_cap()
    {
        await Approve(Ready() with { Line = null });
        _box.HeavySlots = 1;

        Assert.Equal(CheckState.Ok, Row(await Doctor(), "workflow.line.parallel").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_warns_when_the_checkout_has_no_test_file_or_evidence_targets()
    {
        await Approve(Ready());
        _probe.Facts = new LineSetupFacts(TestFiles: false, EvidenceConfig: false, CredentialSet: true);

        var report = await Doctor();

        Assert.Equal(CheckState.Warn, Row(report, "test paths").State);
        Assert.Equal(CheckState.Warn, Row(report, "evidence route").State);
        Assert.False(report.Passed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_wants_a_pin_for_a_pack_that_is_not_built_in_and_takes_the_repo_override()
    {
        await Approve(Ready() with { Packs = new PackSettings(Style: new PackChoice(Default: "plain", Repos: new Dictionary<string, string>(StringComparer.Ordinal) { ["demo"] = "house" })) });
        Assert.Equal(CheckState.Fail, Row(await Doctor(), "workflow.packs.style").State);

        await Approve(Ready() with { Packs = new PackSettings(Style: new PackChoice(Default: "plain", Repos: new Dictionary<string, string>(StringComparer.Ordinal) { ["demo"] = "house" }), Pins: new Dictionary<string, string>(StringComparer.Ordinal) { ["style/house"] = "sha256:abc" }) });
        Assert.Equal(CheckState.Ok, Row(await Doctor(), "workflow.packs.style").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_fails_without_a_forge_adapter_or_a_credential_variable()
    {
        await Approve(Ready() with { Forge = null });
        var noForge = await Doctor();
        Assert.Equal(CheckState.Fail, Row(noForge, "forge credential").State);
        Assert.Equal(CheckState.Fail, Row(noForge, "babysitter CI read").State);

        await Approve(Ready());
        _probe.Facts = _probe.Facts with { CredentialSet = false };
        Assert.Equal(CheckState.Fail, Row(await Doctor(), "forge credential").State);
        Assert.Equal("AW_FORGE_TOKEN", _probe.LastVariable);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_reads_the_variable_a_profile_names_and_trusts_a_command_credential()
    {
        await Approve(Ready() with { Forge = new ForgeSettings(Adapter: "github", Credential: "env:MY_TOKEN") });
        _ = await Doctor();
        Assert.Equal("MY_TOKEN", _probe.LastVariable);

        await Approve(Ready() with { Forge = new ForgeSettings(Adapter: "github", Credential: "command:gh auth token") });
        _probe.Facts = _probe.Facts with { CredentialSet = false };
        Assert.Equal(CheckState.Ok, Row(await Doctor(), "forge credential").State);
        Assert.Null(_probe.LastVariable);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_names_a_repo_the_profile_does_not_know()
    {
        await Approve(Ready());

        var report = await Doctor("/work/other");

        Assert.Null(report.Repo);
        Assert.Equal(CheckState.Fail, Assert.Single(report.Checks).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LineDoctor_refuses_a_directory_outside_git()
    {
        await Approve(Ready());

        var result = await new LineDoctorService(_profiles, _locator, _box, _probe).Handle(new LineDoctorRequest("/elsewhere"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Missing_setup_is_SND_LINE_004_naming_every_failed_row_and_warnings_alone_pass()
    {
        await Approve(Ready() with { Build = null });
        var failed = (await Doctor()).Checks;
        var error = LineDoctorRules.Missing(failed);
        Assert.Equal("SND-LINE-004", error?.Code);
        Assert.Contains("workflow.build.commands", error?.Message, StringComparison.Ordinal);

        await Approve(Ready());
        _probe.Facts = new LineSetupFacts(TestFiles: false, EvidenceConfig: false, CredentialSet: true);
        Assert.Null(LineDoctorRules.Missing((await Doctor()).Checks));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_text_has_one_line_per_check_with_its_fix()
    {
        await Approve(Ready() with { Build = null });

        var text = LineDoctorVerb.Text(await Doctor());

        Assert.Equal(9, text.TrimEnd().Split('\n').Length);
        Assert.Contains("fail  workflow.build.commands", text, StringComparison.Ordinal);
        Assert.Contains("(fix: add workflow.build.commands", text, StringComparison.Ordinal);
    }
}
