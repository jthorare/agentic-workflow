using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// What to do next after an SND-ONYX-011 comes from the command that failed, not from the docker adapter: up, down, status and doctor each know their own fix.
public sealed class ComposeFailureHintTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ToolRun Failed => new(ToolOutcome.Exited, 1, "");

    [Fact, Trait("Lane", "unit")]
    public void A_failed_compose_command_says_what_it_printed_and_leaves_the_next_step_to_the_caller()
    {
        var cli = new DockerCliRuntime(new FakeToolRunner(Failed, ["boom"]), static _ => null);

        var error = cli.Stop(OnyxPaths.For(OnyxUpRig.Home).Project, Token).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Contains("boom", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri onyx up", error.Message, StringComparison.Ordinal);
    }

    private const string Label = "label=com.docker.compose.project=sindri-onyx-0a1b2c3d";

    [Fact, Trait("Lane", "unit")]
    public void A_project_without_files_is_stopped_by_container_id_and_never_through_compose()
    {
        var runner = new FakeToolRunner((_, arguments) => new ToolRun(ToolOutcome.Exited, 0, arguments[0] == "ps" ? "abc123def456\n0123456789ab\n" : ""));

        var stopped = new DockerCliRuntime(runner, static _ => null).Stop(ComposeProject.ByLabel("sindri-onyx-0a1b2c3d"), Token);

        Assert.True(stopped.IsSuccess);
        Assert.Equal(["ps", "-q", "--filter", Label], runner.Calls[0].Arguments, StringComparer.Ordinal);
        Assert.Equal(["stop", "abc123def456", "0123456789ab"], runner.Calls[1].Arguments, StringComparer.Ordinal);
        Assert.Equal(2, runner.Calls.Count);
        Assert.DoesNotContain(runner.Calls, static c => c.Arguments.Contains("compose", StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_project_without_files_and_without_live_containers_stops_nothing()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, ""));

        _ = new DockerCliRuntime(runner, static _ => null).Stop(ComposeProject.ByLabel("sindri-onyx-0a1b2c3d"), Token);

        Assert.Equal(["ps", "-q", "--filter", Label], Assert.Single(runner.Calls).Arguments, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_project_without_files_is_listed_through_docker_ps_by_label()
    {
        var runner = new FakeToolRunner((_, _) => new ToolRun(ToolOutcome.Exited, 0, "abc123def456|running|nginx|nginx@sha256:aaa|127.0.0.1:3000->80/tcp, [::]:3000->80/tcp\n0123456789ab|exited|api_server|onyx@sha256:bbb|\n"));

        var containers = new DockerCliRuntime(runner, static _ => null).Ps(ComposeProject.ByLabel("sindri-onyx-0a1b2c3d"), Token).Value;

        Assert.DoesNotContain(runner.Calls, static c => c.Arguments.Contains("compose", StringComparer.Ordinal));
        Assert.Contains(Label, Assert.Single(runner.Calls).Arguments, StringComparer.Ordinal);
        var nginx = Assert.Single(containers, static c => c.Service == "nginx");
        Assert.Equal("running", nginx.State);
        Assert.Contains(new ComposePublisher("127.0.0.1", 3000, 80), nginx.Publishers);
        Assert.Equal("exited", Assert.Single(containers, static c => c.Service == "api_server").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_up_step_says_to_run_up_again_to_resume()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.FailNext("Pull", SindriErrors.OnyxComposeFailed.With("`docker compose pull` failed (exit 1): no space left on device"));

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.EndsWith("run `sindri onyx up` again to resume", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_names_the_repair_of_onyx_files_when_the_stack_cannot_be_read()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Runtime.FailNext("Ps", SindriErrors.OnyxComposeFailed.With("`docker compose ps` failed (exit 1): no configuration file provided"));

        var check = await new OnyxStackDoctor(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Pins).Check(Token);

        Assert.Equal(CheckState.Warn, check!.State);
        Assert.Contains("run `sindri onyx up` to repair Onyx's files", check.Fix, StringComparison.Ordinal);
    }
}
