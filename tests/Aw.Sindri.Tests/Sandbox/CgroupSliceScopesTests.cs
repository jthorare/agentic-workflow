using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>Live scopes read from the slice's cgroup directory.</summary>
[Lane(TestLane.Unit)]
public sealed class CgroupSliceScopesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-slice-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Live_lists_only_scope_directories()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "sindri-b.scope"));
        Directory.CreateDirectory(Path.Combine(_dir, "sindri-a.scope"));
        Directory.CreateDirectory(Path.Combine(_dir, "other.service"));

        var live = await new CgroupSliceScopes(_dir).Live(TestContext.Current.CancellationToken);

        Assert.Equal(["sindri-a.scope", "sindri-b.scope"], live);
    }

    [Fact]
    public async Task Live_is_empty_when_the_slice_is_not_there()
    {
        var live = await new CgroupSliceScopes(Path.Combine(_dir, "none")).Live(TestContext.Current.CancellationToken);

        Assert.Empty(live);
    }

    [Fact]
    public async Task Live_skips_a_scope_that_holds_only_network_forwarders()
    {
        Scope("sindri-orphan.scope", 10, 11);
        Scope("sindri-session.scope", 20, 21);
        var names = new Dictionary<int, string> { [10] = "socat", [11] = "socat", [20] = "socat", [21] = "claude" };

        var live = await new CgroupSliceScopes(_dir, pid => names.GetValueOrDefault(pid)).Live(TestContext.Current.CancellationToken);

        Assert.Equal(["sindri-session.scope"], live);
    }

    [Fact]
    public async Task Live_skips_a_scope_with_no_processes()
    {
        Scope("sindri-empty.scope");

        var live = await new CgroupSliceScopes(_dir, _ => null).Live(TestContext.Current.CancellationToken);

        Assert.Empty(live);
    }

    private void Scope(string name, params int[] pids)
    {
        var dir = Directory.CreateDirectory(Path.Combine(_dir, name)).FullName;
        File.WriteAllLines(Path.Combine(dir, "cgroup.procs"), pids.Select(p => p.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}
