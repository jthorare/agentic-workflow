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
}
