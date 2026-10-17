using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Health;

public sealed class OnyxStackDoctorTests
{
    private static readonly OnyxPins _pins = new EmbeddedOnyxPins().Pins;
    private static readonly string[] _services = ["api_server", "background", "cache", "inference_model_server", "nginx", "opensearch", "relational_db", "web_server"];

    private readonly FakeContainerRuntime _runtime = new(FakeContainerRuntime.Healthy);
    private readonly FakeOnyxWorkspace _workspace = new() { State = new OnyxSetupState(3000, new Dictionary<string, string>(StringComparer.Ordinal) { [OnyxSteps.Start] = "x" }) };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Pinned(string repository) => repository + "@" + _pins.Images.Single(i => i.Repository == repository && i.Platform == "linux/arm64").Digest;

    private static ComposeContainer Container(string service, string state = "running", string? health = null, string? image = null, ComposePublisher[]? publishers = null)
        => new(service, state, health, image ?? Pinned("docker.io/onyxdotapp/onyx-backend"), publishers ?? (service == "nginx" ? [new ComposePublisher("127.0.0.1", 3000, 80)] : []));

    private OnyxStackDoctor Doctor() => new(_workspace, new OnyxFacts(_runtime, new FakeHostProbes(), new FakeSindriHome(OnyxUpRig.Home)), new FakeOnyxPins(_pins));

    private void Stack(Func<string, ComposeContainer>? make = null)
    {
        _runtime.Containers.Clear();
        foreach (var service in _services)
        {
            _runtime.Containers.Add((make ?? (s => Container(s)))(service));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_state_file_no_row()
    {
        _workspace.State = null;
        Stack();

        Assert.Null(await Doctor().Check(Token));
        Assert.DoesNotContain("Ps", _runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_state_file_warns_and_names_the_fix_without_asking_docker()
    {
        _workspace.StateIsCorrupt = true;
        Stack();

        var check = await Doctor().Check(Token);

        Assert.Equal("onyx:stack", check!.Name);
        Assert.Equal(CheckState.Warn, check.State);
        Assert.Equal("state file unreadable; `sindri onyx up` rebuilds it", check.Detail);
        Assert.Equal("sindri onyx up", check.Fix);
        Assert.DoesNotContain("Ps", _runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_docker_that_answers_but_fails_to_list_the_containers_is_told_apart_from_one_that_is_not_running()
    {
        Stack();
        _runtime.FailNext("Ps", Sindri.Core.SindriErrors.OnyxComposeFailed.With("`docker compose ps` failed (exit 1): no configuration file provided"));

        var failed = await Doctor().Check(Token);
        _runtime.Facts = FakeContainerRuntime.DaemonDown;
        var down = await Doctor().Check(Token);

        Assert.Equal(CheckState.Warn, failed!.State);
        Assert.Contains("SND-ONYX-011", failed.Detail, StringComparison.Ordinal);
        Assert.Contains("no configuration file provided", failed.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("isn't running", failed.Detail, StringComparison.Ordinal);
        Assert.Contains("SND-ONYX-001", down!.Detail, StringComparison.Ordinal);
        Assert.Contains("isn't running", down.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Healthy_loopback_pinned_stack_is_ok()
    {
        Stack(s => Container(s, health: s == "api_server" ? "healthy" : null));

        var check = await Doctor().Check(Token);

        Assert.Equal("onyx:stack", check!.Name);
        Assert.Equal(CheckState.Ok, check.State);
        Assert.Equal("8 containers running, healthy, bound to 127.0.0.1:3000, digests match the pins", check.Detail);
        Assert.Null(check.Fix);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("192.168.1.5")]
    public async Task A_publisher_off_loopback_fails_naming_the_service_the_address_and_the_port(string address)
    {
        Stack(s => s == "nginx" ? Container(s, publishers: [new ComposePublisher(address, 3000, 80)]) : Container(s));

        var check = await Doctor().Check(Token);

        Assert.Equal(CheckState.Fail, check!.State);
        Assert.Contains("nginx", check.Detail, StringComparison.Ordinal);
        Assert.Contains(address, check.Detail, StringComparison.Ordinal);
        Assert.Contains("3000", check.Detail, StringComparison.Ordinal);
        Assert.Contains("sindri onyx down", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unhealthy_container_fails_naming_it()
    {
        Stack(s => Container(s, health: s == "api_server" ? "unhealthy" : null));

        var check = await Doctor().Check(Token);

        Assert.Equal(CheckState.Fail, check!.State);
        Assert.Contains("api_server", check.Detail, StringComparison.Ordinal);
        Assert.Contains("unhealthy", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_container_that_is_not_running_while_others_are_fails_naming_it()
    {
        Stack(s => Container(s, state: s == "web_server" ? "exited" : "running"));

        var check = await Doctor().Check(Token);

        Assert.Equal(CheckState.Fail, check!.State);
        Assert.Contains("web_server", check.Detail, StringComparison.Ordinal);
        Assert.Contains("exited", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_container_on_a_different_digest_fails()
    {
        Stack(s => s == "cache" ? Container(s, image: "docker.io/library/redis@sha256:" + new string('f', 64)) : Container(s));

        var check = await Doctor().Check(Token);

        Assert.Equal(CheckState.Fail, check!.State);
        Assert.Contains("cache", check.Detail, StringComparison.Ordinal);
        Assert.Contains("docker.io/library/redis@sha256:ffff", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_container_whose_image_is_spelled_without_the_default_registry_still_matches()
    {
        Stack(s => Container(s, image: ImageNames.WithoutDefaults(Pinned("docker.io/onyxdotapp/onyx-backend"))));

        Assert.Equal(CheckState.Ok, (await Doctor().Check(Token))!.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stopped_stack_warns_with_the_fix()
    {
        Stack(s => Container(s, state: "exited"));

        var check = await Doctor().Check(Token);

        Assert.Equal(CheckState.Warn, check!.State);
        Assert.Contains("stopped", check.Detail, StringComparison.Ordinal);
        Assert.Equal("sindri onyx up", check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_crash_looping_container_with_the_rest_exited_fails_instead_of_reading_as_stopped()
    {
        Stack(s => Container(s, state: s == "api_server" ? "restarting" : "exited"));

        var check = await Doctor().Check(Token);

        Assert.Equal(CheckState.Fail, check!.State);
        Assert.Contains("api_server", check.Detail, StringComparison.Ordinal);
        Assert.Contains("crash-looping", check.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("stopped", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_set_up_stack_with_no_containers_at_all_is_stopped()
    {
        _runtime.Containers.Clear();

        var check = await Doctor().Check(Token);

        Assert.Equal(CheckState.Warn, check!.State);
        Assert.Equal("sindri onyx up", check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Docker_down_warns()
    {
        Stack();
        _runtime.Facts = FakeContainerRuntime.DaemonDown;

        var check = await Doctor().Check(Token);

        Assert.Equal(CheckState.Warn, check!.State);
        Assert.Contains("docker isn't running", check.Detail, StringComparison.Ordinal);
        Assert.Contains("start Docker", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_permission_denied_socket_warns_with_the_docker_group_fix()
    {
        Stack();
        _runtime.Facts = FakeContainerRuntime.PermissionDenied;

        var check = await Doctor().Check(Token);

        Assert.Equal(CheckState.Warn, check!.State);
        Assert.Contains("add your user to the docker group", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_doctor_run_includes_the_row_after_the_core_rows_only_when_state_exists()
    {
        Stack();
        var core = new Core([new DoctorCheck("context:onyx", CheckState.Ok, "ok", Fix: null)]);

        var with = (await new WithMoreChecks(core, [Doctor()]).Handle(new RunDoctorRequest(), Token)).Value;
        _workspace.State = null;
        var without = (await new WithMoreChecks(core, [Doctor()]).Handle(new RunDoctorRequest(), Token)).Value;

        Assert.Equal(["context:onyx", "onyx:stack"], with.Checks.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.Equal(["context:onyx"], without.Checks.Select(static c => c.Name), StringComparer.Ordinal);
    }

    private sealed class Core(IReadOnlyList<DoctorCheck> checks) : IService<RunDoctorRequest, RunDoctorResponse>
    {
        public ValueTask<Result<RunDoctorResponse>> Handle(RunDoctorRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<RunDoctorResponse>>(new RunDoctorResponse(checks, 0));
    }
}
