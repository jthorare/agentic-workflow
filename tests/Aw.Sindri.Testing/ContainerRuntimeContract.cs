using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IContainerRuntime"/> honours: it reports the facts of the box it asks, and never throws
/// for a missing, stopped or hung runtime. Run against the fake and, over a scripted tool runner, the docker CLI adapter.
/// </summary>
public abstract class ContainerRuntimeContract
{
    /// <summary>A runtime on a box whose facts are <paramref name="facts"/>.</summary>
    /// <param name="facts">The facts the box has.</param>
    /// <returns>The runtime.</returns>
    protected IContainerRuntime Create(ContainerRuntimeFacts facts) => Create(facts, new Dictionary<string, string[]>(StringComparer.Ordinal));

    /// <summary>A runtime on a box whose facts are <paramref name="facts"/> and whose local images are <paramref name="localImages"/>.</summary>
    /// <param name="facts">The facts the box has.</param>
    /// <param name="localImages">Each image reference present on the box, with its RepoDigests as Docker spells them.</param>
    /// <returns>The runtime.</returns>
    protected abstract IContainerRuntime Create(ContainerRuntimeFacts facts, IReadOnlyDictionary<string, string[]> localImages);

    /// <summary>The project the compose cases run against.</summary>
    protected static ComposeProject Project { get; } = new("sindri-onyx-0a1b2c3d", "/o/docker-compose.yml", "/o/docker-compose.override.yml", "/o/.env");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A healthy box reports every fact.</summary>
    [Fact]
    public void A_healthy_box_reports_every_fact()
        => Expect.Equal(FakeContainerRuntime.Healthy, Create(FakeContainerRuntime.Healthy).Inspect(TestContext.Current.CancellationToken));

    /// <summary>No docker CLI reports nothing else either.</summary>
    [Fact]
    public void A_box_without_the_cli_reports_it_missing()
    {
        var facts = FakeContainerRuntime.NoCli;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>The CLI with the daemon down still reports Compose.</summary>
    [Fact]
    public void A_stopped_daemon_is_reported_with_compose_still_known()
    {
        var facts = FakeContainerRuntime.DaemonDown;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>A socket this user may not open is told apart from a stopped daemon.</summary>
    [Fact]
    public void A_socket_permission_problem_is_reported_as_such()
    {
        var facts = FakeContainerRuntime.PermissionDenied;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>A daemon that stays silent past the retry is told apart from a stopped one.</summary>
    [Fact]
    public void A_daemon_that_does_not_answer_in_time_is_reported_as_starting()
    {
        var facts = FakeContainerRuntime.Starting;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>A missing docker context is told apart from a stopped daemon.</summary>
    [Fact]
    public void A_missing_docker_context_is_reported_as_such()
    {
        var facts = FakeContainerRuntime.ContextMissing;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>Only legacy docker-compose: Compose v2 isn't there.</summary>
    [Fact]
    public void A_box_with_only_compose_v1_reports_no_compose_v2()
    {
        var facts = FakeContainerRuntime.ComposeV1Only;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>The server's platform is a linux platform, in the form the lock uses.</summary>
    [Fact]
    public void The_server_platform_is_a_linux_platform_in_the_locks_form()
        => Expect.Equal("linux/arm64", Create(FakeContainerRuntime.Healthy).ServerPlatform(Token).Value);

    /// <summary>Pull, up and stop are ok on a healthy box.</summary>
    [Fact]
    public void Pull_up_stop_are_ok_on_a_healthy_box()
    {
        var runtime = Create(FakeContainerRuntime.Healthy);

        Expect.True(runtime.Pull(Project, static _ => { }, Token).IsSuccess);
        Expect.True(runtime.Up(Project, static _ => { }, limit: null, Token).IsSuccess);
        Expect.True(runtime.Stop(Project, Token).IsSuccess);
    }

    /// <summary>Ps reports what up started, with nginx published on loopback, and stop marks it not running.</summary>
    [Fact]
    public void Ps_reports_what_up_started_and_stop_ends_it()
    {
        var runtime = Create(FakeContainerRuntime.Healthy);
        Expect.True(runtime.Ps(Project, Token).Value.Count == 0);

        _ = runtime.Up(Project, static _ => { }, limit: null, Token);
        var running = runtime.Ps(Project, Token).Value;
        var nginx = running.Single(static c => c.Service == "nginx");
        Expect.Equal("running", nginx.State);
        Expect.Equal(new ComposePublisher("127.0.0.1", 3000, 80), nginx.Publishers.Single());
        Expect.True(running.Any(static c => c.Service == "api_server" && c.State == "running"));

        _ = runtime.Stop(Project, Token);
        Expect.True(runtime.Ps(Project, Token).Value.All(static c => c.State == "exited"));
    }

    /// <summary>The rendered compose's published ports are what a healthy box's Onyx publishes: nginx's 80 on loopback.</summary>
    [Fact]
    public void The_published_ports_of_a_healthy_box_are_nginx_on_loopback()
        => Expect.Equal(new ComposePublishedPort("nginx", "127.0.0.1", 3000, 80), Create(FakeContainerRuntime.Healthy).RenderedConfig(Project, Token).Value.Ports.Single());

    /// <summary>A present image maps to its digests and a missing one to an empty list.</summary>
    [Fact]
    public void A_present_image_maps_to_its_digests_and_a_missing_one_to_none()
    {
        var runtime = Create(FakeContainerRuntime.Healthy, new Dictionary<string, string[]>(StringComparer.Ordinal) { ["docker.io/library/nginx@sha256:aaa"] = ["nginx@sha256:aaa"] });

        var digests = runtime.RepoDigests(["docker.io/library/nginx@sha256:aaa", "docker.io/x/gone@sha256:bbb"], Token).Value;

        Expect.Sequence(["nginx@sha256:aaa"], digests["docker.io/library/nginx@sha256:aaa"]);
        Expect.True(digests["docker.io/x/gone@sha256:bbb"].Count == 0);
    }

    /// <summary>A daemon that isn't answering fails every compose operation with SND-ONYX-001.</summary>
    [Fact]
    public void A_down_daemon_fails_every_compose_operation_with_001()
    {
        var runtime = Create(FakeContainerRuntime.DaemonDown);

        Expect.Equal("SND-ONYX-001", runtime.ServerPlatform(Token).Error.Code);
        Expect.Equal("SND-ONYX-001", runtime.Pull(Project, static _ => { }, Token).Error.Code);
        Expect.Equal("SND-ONYX-001", runtime.Up(Project, static _ => { }, limit: null, Token).Error.Code);
        Expect.Equal("SND-ONYX-001", runtime.Stop(Project, Token).Error.Code);
        Expect.Equal("SND-ONYX-001", runtime.Ps(Project, Token).Error.Code);
        Expect.Equal("SND-ONYX-001", runtime.RenderedConfig(Project, Token).Error.Code);
        Expect.Equal("SND-ONYX-001", runtime.RepoDigests(["a@sha256:b"], Token).Error.Code);
    }

    /// <summary>The memory the containers outside the project use is a plain non-negative byte count.</summary>
    [Fact]
    public void The_memory_other_containers_use_is_a_byte_count()
        => Expect.True(Create(FakeContainerRuntime.Healthy).MemoryUsedOutside(Project, Token).Value >= 0);

    /// <summary>A daemon that isn't answering fails the memory read with SND-ONYX-001.</summary>
    [Fact]
    public void A_down_daemon_fails_the_outside_memory_read_with_001()
        => Expect.Equal("SND-ONYX-001", Create(FakeContainerRuntime.DaemonDown).MemoryUsedOutside(Project, Token).Error.Code);

    /// <summary>A missing CLI fails the same way.</summary>
    [Fact]
    public void A_missing_cli_fails_compose_operations_with_001()
        => Expect.Equal("SND-ONYX-001", Create(FakeContainerRuntime.NoCli).Stop(Project, Token).Error.Code);
}
