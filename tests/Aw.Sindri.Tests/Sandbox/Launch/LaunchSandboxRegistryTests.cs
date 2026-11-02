using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Egress;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The running egress of each launch (sandbox spec §3.2): started before the self-check, disposed at release.</summary>
public sealed class LaunchSandboxRegistryTests : IDisposable
{
    private static readonly LaunchId _one = LaunchId.From("LCH-00000000000000000000000000000001");
    private static readonly LaunchId _two = LaunchId.From("LCH-00000000000000000000000000000002");

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Socat()
    {
        var socat = Path.Combine(_temp.Path, "socat");
        File.WriteAllText(socat, "#!/bin/sh\n");
        return socat;
    }

    private LaunchSandboxRegistry Registry(bool socat = true)
    {
        var path = socat ? Socat() : "/nonexistent/socat";
        var service = new EgressService(
            new FakeEgressResolver(new Dictionary<string, System.Net.IPAddress[]>(StringComparer.Ordinal)),
            new FakeEgressDialer(),
            n => n == SandboxEnvironment.Socat ? path : null,
            Path.Combine(_temp.Path, "run"));
        return new LaunchSandboxRegistry(service);
    }

    private PlanNetwork Net(string name) => new(NetworkMode.Enforce, [], 7480, 3128, Path.Combine(_temp.Path, name));

    [Fact, Lane(TestLane.Integration)]
    public async Task Start_registers_the_egress_under_the_launch()
    {
        await using var registry = Registry();

        var started = await registry.Start(_one, Net("n1"), denials: null);

        Assert.False(started.IsError);
        Assert.Same(started.Value, registry.Get(_one));
        Assert.Null(registry.Get(_two));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_refused_start_registers_nothing_and_keeps_the_refusal()
    {
        await using var registry = Registry(socat: false);

        var started = await registry.Start(_one, Net("n1"), denials: null);

        Assert.True(started.IsError);
        Assert.Equal(SindriErrors.LaunchSandboxUnavailable.Code, started.Error.Code);
        Assert.Null(registry.Get(_one));
    }

    [Fact, Lane(TestLane.Integration)]
    public async Task Release_disposes_the_egress_and_removes_its_socket_directory()
    {
        await using var registry = Registry();
        var net = Net("n1");
        _ = await registry.Start(_one, net, denials: null);
        Assert.True(Directory.Exists(net.NetDir));

        Assert.True(await registry.Release(_one));

        Assert.Null(registry.Get(_one));
        Assert.False(Directory.Exists(net.NetDir));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task Releasing_a_launch_with_nothing_started_is_a_no_op()
    {
        await using var registry = Registry();

        Assert.False(await registry.Release(_one));
    }

    [Fact, Lane(TestLane.Integration)]
    public async Task Releasing_twice_disposes_once()
    {
        await using var registry = Registry();
        _ = await registry.Start(_one, Net("n1"), denials: null);

        Assert.True(await registry.Release(_one));
        Assert.False(await registry.Release(_one));
    }

    [Fact, Lane(TestLane.Integration)]
    public async Task Disposing_the_registry_releases_every_launch()
    {
        var registry = Registry();
        var a = Net("n1");
        var b = Net("n2");
        _ = await registry.Start(_one, a, denials: null);
        _ = await registry.Start(_two, b, denials: null);

        await registry.DisposeAsync();

        Assert.False(Directory.Exists(a.NetDir));
        Assert.False(Directory.Exists(b.NetDir));
    }

    [Fact, Lane(TestLane.Integration)]
    public async Task Concurrent_starts_and_releases_leave_nothing_behind()
    {
        await using var registry = Registry();
        var ids = Enumerable.Range(1, 8).Select(i => LaunchId.From($"LCH-{i:x32}")).ToArray();

        await Task.WhenAll(ids.Select(async (id, i) =>
        {
            _ = await registry.Start(id, Net("c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)), denials: null);
            await Task.Yield();
            _ = await registry.Release(id);
        }));

        Assert.All(ids, id => Assert.Null(registry.Get(id)));
    }

    [Fact, Lane(TestLane.Integration)]
    public async Task Starting_a_launch_again_replaces_and_disposes_the_old_egress()
    {
        await using var registry = Registry();
        var first = Net("n1");
        _ = await registry.Start(_one, first, denials: null);

        var second = await registry.Start(_one, Net("n2"), denials: null);

        Assert.False(second.IsError);
        Assert.False(Directory.Exists(first.NetDir));
        Assert.Same(second.Value, registry.Get(_one));
    }
}
