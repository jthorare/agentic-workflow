using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class OnyxPreflightBlocksTests
{
    private const long Gib = 1L << 30;

    private static readonly OnyxPins _pins = new("v4.9.0", new string('0', 40), [], [], DiskBytes: 30 * Gib, DockerMemoryBytes: 5_905_580_032);
    private static readonly DiskFacts _roomy = new("/h", 80 * Gib);
    private static readonly PortFacts _free = new(3000, PreferredFree: true, FirstFreeFallback: null);

    private static PreflightReport Evaluate(ContainerRuntimeFacts docker, DiskFacts? home = null, PortFacts? port = null, int? ownPort = null)
        => OnyxPreflight.Evaluate(docker, new HostFacts(home ?? _roomy, DataRoot: null, port ?? _free), _pins, ownPort);

    private static PreflightCheck Check(PreflightReport report, string name) => Assert.Single(report.Checks, c => c.Name == name);

    [Fact, Trait("Lane", "unit")]
    public void Blocks_is_true_only_for_a_fail()
    {
        Assert.False(Evaluate(FakeContainerRuntime.Healthy).Blocks);
        Assert.False(Evaluate(FakeContainerRuntime.Healthy with { MemoryBytes = null }).Blocks);
        Assert.True(Evaluate(FakeContainerRuntime.Healthy, home: new DiskFacts("/h", Gib)).Blocks);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tcp_endpoint_fails_the_docker_row_with_010()
    {
        var report = Evaluate(FakeContainerRuntime.Healthy with { Endpoint = "tcp://10.0.0.5:2375" });

        var docker = Check(report, "docker");
        Assert.Equal(CheckState.Fail, docker.State);
        Assert.Equal("SND-ONYX-010", docker.Problem?.Code);
        Assert.Contains("Docker points at another machine (tcp://10.0.0.5:2375)", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.Contains("switch to the local context", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.True(report.Blocks);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_null_endpoint_fails_with_010_saying_it_could_not_be_read()
    {
        var docker = Check(Evaluate(FakeContainerRuntime.Healthy with { Endpoint = null }), "docker");

        Assert.Equal("SND-ONYX-010", docker.Problem?.Code);
        Assert.Contains("couldn't read Docker's endpoint (`docker context inspect` failed); check `docker context ls`", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("another machine", docker.Problem?.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("unix:///var/run/docker.sock")]
    [InlineData("unix:///Users/me/.docker/run/docker.sock")]
    [InlineData("npipe:////./pipe/docker_engine")]
    public void An_unix_and_an_npipe_endpoint_pass(string endpoint)
        => Assert.Equal(CheckState.Ok, Check(Evaluate(FakeContainerRuntime.Healthy with { Endpoint = endpoint }), "docker").State);

    [Fact, Trait("Lane", "unit")]
    public void A_missing_cli_is_001_before_the_endpoint_is_judged()
        => Assert.Equal("SND-ONYX-001", Check(Evaluate(FakeContainerRuntime.NoCli), "docker").Problem?.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_remote_endpoint_is_010_even_when_the_daemon_is_down()
        => Assert.Equal("SND-ONYX-010", Check(Evaluate(FakeContainerRuntime.DaemonDown with { Endpoint = "ssh://me@host" }), "docker").Problem?.Code);

    [Fact, Trait("Lane", "unit")]
    public void Own_port_makes_the_port_row_ok_even_when_taken()
    {
        var report = Evaluate(FakeContainerRuntime.Healthy, port: new PortFacts(3000, PreferredFree: false, FirstFreeFallback: null), ownPort: 3002);

        var port = Check(report, "port");
        Assert.Equal(CheckState.Ok, port.State);
        Assert.Equal("3002 (Onyx's own)", port.Detail);
        Assert.Equal(3002, report.Port);
        Assert.False(report.Blocks);
    }
}
