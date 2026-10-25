using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// Which runtime serves the CLI (OrbStack, Docker Desktop, or one Sindri doesn't recognize) and the fix the docker row gives for it when the daemon doesn't answer.
public sealed class DockerRuntimeKindTests
{
    private const long Gib = 1L << 30;
    private const string OrbStackSocket = "unix:///Users/u/.orbstack/run/docker.sock";
    private const string DesktopSocket = "unix:///Users/u/.docker/run/docker.sock";

    private static readonly OnyxPins _pins = new("v4.9.0", new string('0', 40), [], [], DiskBytes: 30 * Gib, DockerMemoryBytes: 5_905_580_032);

    private static PreflightCheck DockerRow(ContainerRuntimeFacts facts)
        => Assert.Single(OnyxPreflight.Evaluate(facts, new HostFacts(new DiskFacts("/h", 80 * Gib), DataRoot: null, new PortFacts(3189, PreferredFree: true, FirstFreeFallback: 3190)), _pins).Checks, static c => c.Name == "docker");

    [Theory, Trait("Lane", "unit")]
    [InlineData(OrbStackSocket, ContainerRuntimeKind.OrbStack)]
    [InlineData("unix:///Users/u/.orbstack/run/docker.sock.real", ContainerRuntimeKind.OrbStack)]
    [InlineData(DesktopSocket, ContainerRuntimeKind.DockerDesktop)]
    [InlineData("unix:///home/u/.docker/desktop/docker.sock", ContainerRuntimeKind.DockerDesktop)]
    [InlineData("npipe:////./pipe/dockerDesktopLinuxEngine", ContainerRuntimeKind.DockerDesktop)]
    [InlineData("unix:///var/run/docker.sock", ContainerRuntimeKind.Docker)]
    [InlineData("unix:///Users/u/.colima/default/docker.sock", ContainerRuntimeKind.Docker)]
    [InlineData(null, ContainerRuntimeKind.Docker)]
    public void A_daemon_that_does_not_answer_is_known_by_its_socket_path(string? endpoint, ContainerRuntimeKind kind)
        => Assert.Equal(kind, (FakeContainerRuntime.Starting with { Endpoint = endpoint }).Kind);

    [Theory, Trait("Lane", "unit")]
    [InlineData("OrbStack", ContainerRuntimeKind.OrbStack)]
    [InlineData("Docker Desktop", ContainerRuntimeKind.DockerDesktop)]
    [InlineData("Ubuntu 24.04.1 LTS", ContainerRuntimeKind.Docker)]
    public void A_daemon_that_answers_is_known_by_its_operating_system_whatever_the_socket(string operatingSystem, ContainerRuntimeKind kind)
        => Assert.Equal(kind, (FakeContainerRuntime.Healthy with { Endpoint = "unix:///var/run/docker.sock", OperatingSystem = operatingSystem }).Kind);

    [Fact, Trait("Lane", "unit")]
    public void A_hung_OrbStack_is_told_to_open_OrbStack_or_run_orb_restart()
    {
        var row = DockerRow(FakeContainerRuntime.Starting with { Endpoint = OrbStackSocket });

        Assert.Equal(CheckState.Fail, row.State);
        Assert.Equal("OrbStack didn't answer within 25 s; it may be starting", row.Detail);
        Assert.Equal("SND-ONYX-001", row.Problem?.Code);
        Assert.Equal("OrbStack isn't answering (no reply within 25 s): open OrbStack, or run `orb restart` if it says Running, then run this again", row.Problem?.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hung_Docker_Desktop_is_told_it_may_be_waking_from_Resource_Saver()
    {
        var row = DockerRow(FakeContainerRuntime.Starting with { Endpoint = DesktopSocket });

        Assert.Equal("Docker Desktop didn't answer within 25 s; it may be starting", row.Detail);
        Assert.Equal("Docker Desktop isn't answering (no reply within 25 s): open Docker Desktop (it may be waking from Resource Saver) and wait until it is ready, or restart it from its menu, then run this again", row.Problem?.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hung_docker_nobody_recognizes_names_the_runtimes_it_could_be()
        => Assert.Equal(
            "Docker isn't answering (no reply within 25 s): restart it (Docker Desktop, OrbStack, or the docker service), then run this again",
            DockerRow(FakeContainerRuntime.Starting).Problem?.Message);

    [Theory, Trait("Lane", "unit")]
    [InlineData(OrbStackSocket, "OrbStack isn't running: open OrbStack (or run `orb start`) and wait until it is ready, then run this again")]
    [InlineData(DesktopSocket, "Docker Desktop isn't running: open Docker Desktop and wait until it is ready, then run this again")]
    [InlineData("unix:///var/run/docker.sock", "Docker isn't running: start Docker Desktop, OrbStack or the docker service and wait until it is ready, then run this again")]
    public void A_daemon_that_is_not_running_names_its_runtime_and_how_to_start_it(string endpoint, string message)
        => Assert.Equal(message, DockerRow(FakeContainerRuntime.DaemonDown with { Endpoint = endpoint }).Problem?.Message);

    [Fact, Trait("Lane", "unit")]
    public void A_daemon_that_is_not_running_after_a_wait_says_how_long_it_was_given()
        => Assert.Equal("docker CLI found, daemon not answering after 180 s", DockerRow(FakeContainerRuntime.DaemonDown with { WaitedSeconds = 180 }).Detail);

    [Theory, Trait("Lane", "unit")]
    [InlineData("starting", true)]
    [InlineData("not running", true)]
    [InlineData("answers", false)]
    [InlineData("no cli", false)]
    [InlineData("permission denied", false)]
    [InlineData("context missing", false)]
    [InlineData("remote", false)]
    public void Only_a_local_daemon_that_is_starting_or_not_running_may_still_come_up(string box, bool mayStart)
        => Assert.Equal(mayStart, (box switch
        {
            "starting" => FakeContainerRuntime.Starting,
            "not running" => FakeContainerRuntime.DaemonDown,
            "answers" => FakeContainerRuntime.Healthy,
            "no cli" => FakeContainerRuntime.NoCli,
            "permission denied" => FakeContainerRuntime.PermissionDenied,
            "context missing" => FakeContainerRuntime.ContextMissing,
            _ => FakeContainerRuntime.Starting with { Endpoint = "ssh://me@host" },
        }).DaemonMayStillStart);
}
