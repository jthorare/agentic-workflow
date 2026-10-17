using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The compose project is per Sindri home, named in one place, recorded in state.json, and what status, doctor and down act on.
public sealed class OnyxProjectNameTests
{
    private const string Recorded = "sindri-onyx-deadbeef";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<OnyxUpRig> Running(string? project = Recorded)
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Workspace.State = rig.Workspace.State! with { Project = project };
        rig.Runtime.Projects.Clear();
        return rig;
    }

    [Fact, Trait("Lane", "unit")]
    public void The_name_is_sindri_onyx_and_the_first_8_hex_of_the_sha256_of_the_home_path()
    {
        var name = ComposeProject.NameFor("/home/joi/.agentic-workflow/sindri-app");

        Assert.True(ComposeProject.IsOurs(name));
        Assert.Equal("sindri-onyx-", name[..12], StringComparer.Ordinal);
        Assert.Equal(20, name.Length);
        Assert.Equal(name, ComposeProject.NameFor("/home/joi/.agentic-workflow/sindri-app/"), StringComparer.Ordinal);
        Assert.NotEqual(name, ComposeProject.NameFor("/tmp/aw-onyx-real-1"), StringComparer.Ordinal);
        Assert.Equal(name, OnyxPaths.For("/home/joi/.agentic-workflow/sindri-app").Project.Name, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_records_the_project_name_in_state_json()
    {
        var rig = new OnyxUpRig();

        _ = (await rig.Up(Token)).Value;

        Assert.Equal(rig.Paths.Project.Name, rig.Workspace.State!.Project);
        Assert.All(rig.Runtime.Projects, name => Assert.Equal(rig.Paths.Project.Name, name));
    }

    private const string Foreign = "state names another home's project; ignoring it";

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_with_this_homes_own_recorded_name_manages_it_without_a_warning()
    {
        var rig = await Running(new OnyxUpRig().Paths.Project.Name);

        _ = (await rig.Up(Token)).Value;

        Assert.NotEmpty(rig.Runtime.Projects);
        Assert.All(rig.Runtime.Projects, name => Assert.Equal(rig.Paths.Project.Name, name));
        Assert.DoesNotContain(rig.Progress.Lines, static l => l.Contains(Foreign, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_refuses_a_project_name_recorded_by_another_home_warns_and_manages_its_own()
    {
        var rig = await Running();

        _ = (await rig.Up(Token)).Value;

        Assert.NotEmpty(rig.Runtime.Projects);
        Assert.All(rig.Runtime.Projects, name => Assert.Equal(rig.Paths.Project.Name, name));
        Assert.Equal(rig.Paths.Project.Name, rig.Workspace.State!.Project);
        Assert.Contains(rig.Progress.Lines, static l => l.Contains(Foreign, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_state_that_names_another_home_is_not_trusted_for_its_step_stamps_either()
    {
        var rig = await Running();

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["fetch", "config", "pull", "start"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(1, rig.Progress.Lines.Count(static l => l.Contains(Foreign, StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recorded_name_that_is_not_ours_is_ignored()
    {
        var rig = await Running("--help");

        _ = (await rig.Up(Token)).Value;

        Assert.All(rig.Runtime.Projects, name => Assert.Equal(rig.Paths.Project.Name, name));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_stops_this_homes_project_not_a_name_recorded_by_another_home_and_says_so()
    {
        var rig = await Running();

        var response = (await new OnyxDownService(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Runtime).Handle(new OnyxDownRequest(), Token)).Value;

        Assert.NotEmpty(rig.Runtime.Projects);
        Assert.All(rig.Runtime.Projects, name => Assert.Equal(rig.Paths.Project.Name, name));
        Assert.Contains(Foreign, response.Warnings, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_reads_this_homes_project_not_a_name_recorded_by_another_home()
    {
        var rig = await Running();

        _ = (await new OnyxStatusService(rig.Pins, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Health, rig.Workspace).Handle(new OnyxStatusRequest(), Token)).Value;

        Assert.NotEmpty(rig.Runtime.Projects);
        Assert.All(rig.Runtime.Projects, name => Assert.Equal(rig.Paths.Project.Name, name));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_reads_this_homes_project_not_a_name_recorded_by_another_home()
    {
        var rig = await Running();

        _ = await new OnyxStackDoctor(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Pins).Check(Token);

        Assert.NotEmpty(rig.Runtime.Projects);
        Assert.All(rig.Runtime.Projects, name => Assert.Equal(rig.Paths.Project.Name, name));
    }
}
