using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxEnsure;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxEnsure;

// Spec §4.2: serve brings a managed Onyx up in the background, never fetches or pulls, and an unmanaged Onyx is never touched.
public sealed class OnyxEnsureTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig
    {
        public Rig()
        {
            Managed.Managed = true;
            Workspace.State = new OnyxSetupState(3189, new Dictionary<string, string>(StringComparer.Ordinal) { [OnyxSteps.Start] = "x" });
            foreach (var path in new[] { Paths.Compose, Paths.Override, Paths.Env })
            {
                Workspace.Files[path] = "x";
            }
        }

        public OnyxUpRig Up { get; } = new();

        public FakeManagedOnyx Managed { get; } = new();

        public FakeOnyxWorkspace Workspace => Up.Workspace;

        public OnyxPaths Paths => Up.Paths;

        public OnyxEnsureService Service()
            => new(Managed, Workspace, new OnyxFacts(Up.Runtime, Up.Probes, new FakeSindriHome(OnyxUpRig.Home)), Up.Runtime, Up.Health, Up.Clock);

        public async Task<Kernel.Result<OnyxEnsureResponse>> Run() => await Service().Handle(new OnyxEnsureRequest(), Token);

        public string Note => Workspace.Files[Paths.Ensure].Trim();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unmanaged_onyx_is_never_touched()
    {
        var rig = new Rig();
        rig.Managed.Managed = false;

        var response = (await rig.Run()).Value;

        Assert.Null(response.Endpoint);
        Assert.Empty(rig.Up.Runtime.Calls);
        Assert.False(rig.Workspace.Files.ContainsKey(rig.Paths.Ensure));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_box_where_up_never_finished_gets_the_needs_setup_note_and_nothing_is_started()
    {
        var rig = new Rig();
        rig.Workspace.State = null;

        var response = (await rig.Run()).Value;

        Assert.Equal("needs-setup", response.Endpoint!.State);
        Assert.Equal("needs-setup", rig.Note);
        Assert.DoesNotContain("Up", rig.Up.Runtime.Calls, StringComparer.Ordinal);
        Assert.DoesNotContain("Pull", rig.Up.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stack_that_is_down_is_started_without_a_pull_or_a_fetch_and_recorded_as_running()
    {
        var rig = new Rig();

        var response = (await rig.Run()).Value;

        Assert.Equal("running", response.Endpoint!.State);
        Assert.Equal("http://127.0.0.1:3189", response.Endpoint.Url);
        Assert.StartsWith("sindri-onyx-", response.Endpoint.Project, StringComparison.Ordinal);
        Assert.Contains("Up", rig.Up.Runtime.Calls, StringComparer.Ordinal);
        Assert.DoesNotContain("Pull", rig.Up.Runtime.Calls, StringComparer.Ordinal);
        Assert.Equal("running", rig.Note);
        Assert.Equal(0, rig.Up.Files.Fetches);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stack_that_already_runs_is_left_alone()
    {
        var rig = new Rig();
        _ = rig.Up.Runtime.Up(rig.Paths.Project, static _ => { }, limit: null, Token);
        rig.Up.Runtime.Calls.Clear();

        var response = (await rig.Run()).Value;

        Assert.Equal("running", response.Endpoint!.State);
        Assert.DoesNotContain("Up", rig.Up.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_docker_that_is_not_answering_is_a_failed_note_not_an_error()
    {
        var rig = new Rig();
        rig.Up.Runtime.Facts = FakeContainerRuntime.DaemonDown;

        var result = await rig.Run();

        Assert.True(result.IsSuccess);
        Assert.Equal("failed", result.Value.Endpoint!.State);
        Assert.StartsWith("failed:", rig.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("Up", rig.Up.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Missing_files_are_a_failed_note_that_names_onyx_up()
    {
        var rig = new Rig();
        _ = rig.Workspace.Files.Remove(rig.Paths.Override);

        var response = (await rig.Run()).Value;

        Assert.Equal("failed", response.Endpoint!.State);
        Assert.Contains("sindri onyx up", response.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_compose_up_that_fails_is_a_failed_note()
    {
        var rig = new Rig();
        rig.Up.Runtime.FailNext("Up", SindriErrors.OnyxComposeFailed.With("port is already allocated"));

        var response = (await rig.Run()).Value;

        Assert.Equal("failed", response.Endpoint!.State);
        Assert.Contains("port is already allocated", rig.Note, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_start_interrupted_by_serve_stopping_leaves_no_failed_note()
    {
        var rig = new Rig();
        using var stopping = new CancellationTokenSource();
        rig.Up.Runtime.FailNext("Up", SindriErrors.OnyxComposeFailed.With("cancelled"));
        await stopping.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await rig.Service().Handle(new OnyxEnsureRequest(), stopping.Token));

        Assert.False(rig.Workspace.Files.ContainsKey(rig.Paths.Ensure));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_onyx_that_never_answers_fails_after_five_minutes()
    {
        var rig = new Rig();
        rig.Up.Health.Default = new OnyxProbe(Healthy: false, BackendVersion: null);
        rig.Up.Clock.AdvanceOnDelay = true;

        var response = (await rig.Run()).Value;

        Assert.Equal("failed", response.Endpoint!.State);
        Assert.Contains("5 minutes", response.Detail, StringComparison.Ordinal);
    }
}
