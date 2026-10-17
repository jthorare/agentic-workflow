using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxDown;

public sealed class OnyxDownTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OnyxDownService Service(OnyxUpRig rig)
        => new(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Runtime);

    private static async Task<Kernel.Result<OnyxDownResponse>> Down(OnyxUpRig rig)
        => await Service(rig).Handle(new OnyxDownRequest(), Token);

    private static async Task<OnyxUpRig> RunningRig()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        return rig;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_stops_the_project_and_keeps_the_files()
    {
        var rig = await RunningRig();
        var (files, state, stateWrites) = (rig.Workspace.Files.ToDictionary(static f => f.Key, static f => f.Value, StringComparer.Ordinal), rig.Workspace.State, rig.Workspace.StateWrites);

        var response = (await Down(rig)).Value;

        Assert.True(response.WasRunning);
        Assert.Contains("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
        Assert.All(rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
        Assert.Equal(files.Count, rig.Workspace.Files.Count);
        Assert.All(files, f => Assert.Equal(f.Value, rig.Workspace.Files[f.Key], StringComparer.Ordinal));
        Assert.Same(state, rig.Workspace.State);
        Assert.Equal(stateWrites, rig.Workspace.StateWrites);
        Assert.False(rig.Workspace.LockHeld);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_on_a_stack_that_is_already_stopped_is_ok_and_says_it_was_not_running()
    {
        var rig = await RunningRig();
        _ = (await Down(rig)).Value;

        var again = (await Down(rig)).Value;

        Assert.False(again.WasRunning);
    }

    // A start that didn't finish: containers up, no `start` step in state.json.
    private static async Task<OnyxUpRig> FailedStartRig()
    {
        var rig = await RunningRig();
        var state = rig.Workspace.State!;
        rig.Workspace.State = state with { Steps = state.Steps.Where(static s => s.Key != "start").ToDictionary(static s => s.Key, static s => s.Value, StringComparer.Ordinal) };
        return rig;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_after_a_failed_start_stops_the_containers_the_project_has()
    {
        var rig = await FailedStartRig();

        var response = (await Down(rig)).Value;

        Assert.True(response.WasRunning);
        Assert.Contains("nginx", response.Stopped, StringComparer.Ordinal);
        Assert.Contains("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
        Assert.All(rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_a_corrupt_state_still_stops_running_containers()
    {
        var rig = await RunningRig();
        rig.Workspace.StateIsCorrupt = true;

        var response = (await Down(rig)).Value;

        Assert.True(response.WasRunning);
        Assert.All(rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_says_nothing_was_running_only_when_ps_shows_no_running_container()
    {
        var rig = await FailedStartRig();
        rig.Runtime.Containers.Clear();

        var response = (await Down(rig)).Value;

        Assert.False(response.WasRunning);
        Assert.Empty(response.Stopped);
        Assert.DoesNotContain("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("restarting")]
    [InlineData("paused")]
    public async Task Down_stops_a_project_whose_only_live_container_is_not_in_the_running_state(string state)
    {
        var rig = await RunningRig();
        for (var i = 0; i < rig.Runtime.Containers.Count; i++)
        {
            rig.Runtime.Containers[i] = rig.Runtime.Containers[i] with { State = rig.Runtime.Containers[i].Service == "api_server" ? state : "exited" };
        }

        var response = (await Down(rig)).Value;

        Assert.True(response.WasRunning);
        Assert.Equal(["api_server"], response.Stopped);
        Assert.Contains("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_an_unreadable_compose_file_reports_SND_ONYX_012_and_stops_nothing()
    {
        var rig = await RunningRig();
        _ = rig.Workspace.Unreadable.Add(rig.Paths.Compose);

        var result = await Down(rig);

        Assert.Equal("SND-ONYX-012", result.IsError ? result.Error.Code : "no error: down said WasRunning=" + result.Value.WasRunning);
        Assert.DoesNotContain("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_on_a_box_never_set_up_is_a_no_op()
    {
        var rig = new OnyxUpRig();

        var response = (await Down(rig)).Value;

        Assert.False(response.WasRunning);
        Assert.DoesNotContain("Stop", rig.Runtime.Calls, StringComparer.Ordinal);   // it only asks docker by the project label: containers outlive a deleted directory
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_refuses_a_remote_endpoint_with_010()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = "tcp://10.0.0.5:2375" };

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-010", error.Code);
        Assert.Contains("tcp://10.0.0.5:2375", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_no_docker_cli_is_001_with_the_install_fix_not_a_remote_refusal()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.NoCli;

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-001", error.Code);
        Assert.Contains("Docker isn't installed", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_a_daemon_that_does_not_answer_is_001_isnt_running()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.DaemonDown;

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-001", error.Code);
        Assert.Contains("Docker isn't running", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_a_permission_denied_socket_gives_the_docker_group_fix()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.PermissionDenied;

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-001", error.Code);
        Assert.Contains("add your user to the docker group", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_a_daemon_that_may_be_starting_says_to_wait()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.Starting;

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-001", error.Code);
        Assert.Contains("may still be starting", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_an_unreadable_endpoint_says_so_under_010()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = null };

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-010", error.Code);
        Assert.Contains("couldn't read Docker's endpoint (`docker context inspect` failed); check `docker context ls`", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("another machine", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_reports_a_failed_stop_as_011()
    {
        var rig = await RunningRig();
        rig.Runtime.FailNext("Stop", SindriErrors.OnyxComposeFailed.With("docker compose stop failed"));

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_docker_not_answering_is_001()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.DaemonDown;

        Assert.Equal("SND-ONYX-001", (await Down(rig)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_while_another_onyx_command_holds_the_lock_is_busy()
    {
        var rig = await RunningRig();
        using var held = rig.Workspace.TryLock().Value;

        Assert.Equal("SND-API-009", (await Down(rig)).Error.Code);
    }
}
