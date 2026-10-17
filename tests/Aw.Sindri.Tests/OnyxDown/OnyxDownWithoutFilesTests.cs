using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxDown;

// With the compose file there but Sindri's override or .env gone, compose can't read the project's files; the containers are still stopped by the project label.
public sealed class OnyxDownWithoutFilesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<Kernel.Result<OnyxDownResponse>> Down(OnyxUpRig rig)
        => await new OnyxDownService(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Runtime).Handle(new OnyxDownRequest(), Token);

    private static async Task<OnyxUpRig> RunningRigWithout(Func<OnyxPaths, string> file)
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        Assert.True(rig.Workspace.Files.Remove(file(rig.Paths)));
        rig.Runtime.Calls.Clear();
        rig.Runtime.LabelOnlyCalls.Clear();
        return rig;
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("override")]
    [InlineData("env")]
    public async Task Down_stops_the_live_containers_by_project_label_when_a_rendered_file_is_missing(string missing)
    {
        var rig = await RunningRigWithout(missing == "override" ? static p => p.Override : static p => p.Env);

        var response = (await Down(rig)).Value;

        Assert.True(response.WasRunning);
        Assert.Contains("nginx", response.Stopped, StringComparer.Ordinal);
        Assert.Equal(["Ps", "Stop", "Ps"], rig.Runtime.LabelOnlyCalls);   // list, stop, then ask again what is down
        Assert.Contains(response.Warnings, static w => w.Contains("project label", StringComparison.Ordinal));
        Assert.All(rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_stops_the_live_containers_by_project_label_when_the_compose_file_is_gone_too()
    {
        var rig = await RunningRigWithout(static p => p.Compose);

        var response = (await Down(rig)).Value;

        Assert.True(response.WasRunning);
        Assert.Contains("nginx", response.Stopped, StringComparer.Ordinal);
        Assert.Equal(["Ps", "Stop", "Ps"], rig.Runtime.LabelOnlyCalls);   // list, stop, then ask again what is down
        Assert.Contains(response.Warnings, static w => w.Contains("project label", StringComparison.Ordinal));
        Assert.All(rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_no_file_and_nothing_live_does_nothing_and_says_nothing()
    {
        var rig = new OnyxUpRig();

        var response = (await Down(rig)).Value;

        Assert.False(response.WasRunning);
        Assert.Empty(response.Warnings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_by_label_reports_only_what_stopped_and_warns_about_what_is_still_live()
    {
        var rig = await RunningRigWithout(static p => p.Override);
        rig.Runtime.StubbornServices.Add("nginx");

        var response = (await Down(rig)).Value;

        Assert.True(response.WasRunning);
        Assert.DoesNotContain("nginx", response.Stopped, StringComparer.Ordinal);
        Assert.Contains("api_server", response.Stopped, StringComparer.Ordinal);
        Assert.Contains(response.Warnings, static w => w.Contains("still running", StringComparison.Ordinal) && w.Contains("nginx", StringComparison.Ordinal));
        Assert.Equal(["Ps", "Stop", "Ps"], rig.Runtime.LabelOnlyCalls);
    }

    // The same decision serves the start's own-port check: with any file gone, our running nginx is looked for by the project label.
    [Theory, Trait("Lane", "unit")]
    [InlineData("compose")]
    [InlineData("override")]
    [InlineData("env")]
    public async Task The_project_in_use_is_by_label_when_any_one_of_the_three_files_is_gone(string missing)
    {
        var rig = await RunningRigWithout(missing switch { "compose" => static p => p.Compose, "override" => static p => p.Override, _ => static p => p.Env });
        var facts = new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home));

        var (project, gone) = facts.ProjectInUse(rig.Workspace).Value;

        Assert.False(project.HasFiles);
        Assert.Equal(facts.Project.Name, project.Name);
        _ = Assert.Single(gone);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_project_in_use_has_its_files_when_all_three_are_there()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        var facts = new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home));

        var (project, gone) = facts.ProjectInUse(rig.Workspace).Value;

        Assert.True(project.HasFiles);
        Assert.Empty(gone);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_the_files_present_still_uses_them()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Runtime.LabelOnlyCalls.Clear();   // the first up asked by label before any file existed

        _ = (await Down(rig)).Value;

        Assert.Empty(rig.Runtime.LabelOnlyCalls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_whose_label_stop_fails_is_011_with_the_down_fix()
    {
        var rig = await RunningRigWithout(static p => p.Override);
        rig.Runtime.FailNext("Stop", SindriErrors.OnyxComposeFailed.With("`docker compose stop` failed (exit 1): boom"));

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Contains("run `sindri onyx down` again", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri onyx up", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_down_never_says_to_run_up()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Runtime.FailNext("Stop", SindriErrors.OnyxComposeFailed.With("`docker compose stop` failed (exit 1): boom"));

        var error = (await Down(rig)).Error;

        Assert.Contains("run `sindri onyx down` again", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri onyx up", error.Message, StringComparison.Ordinal);
    }
}
