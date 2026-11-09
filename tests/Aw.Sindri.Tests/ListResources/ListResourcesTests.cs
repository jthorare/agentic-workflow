using Aw.Kernel.Testing;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Features.ListResources;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.ListResources;

/// <summary>'sindri resources list': owner, kind, name, size and path of what was provisioned.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ListResourcesTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly ListResourcesService _service;

    public ListResourcesTests()
    {
        _service = Build();
    }

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_home.Path, "resources");

    [Fact]
    public async Task Every_owner_lists_its_databases_and_disks_with_size_and_path()
    {
        Provision("workflow:triage");
        Provision("node:triage/fetch");
        Provision("toolkit:mail");
        await File.WriteAllBytesAsync(Path.Combine(Root, "workflow", "triage", "scratch", "a.bin"), new byte[300], Token);
        await File.WriteAllBytesAsync(Path.Combine(Root, "workflow", "triage", "state.db"), new byte[40], Token);

        var listed = await _service.Handle(new ListResourcesRequest(), Token);

        Assert.Equal(
            [
                "node:triage/fetch database state", "node:triage/fetch disk scratch",
                "toolkit:mail database state", "toolkit:mail disk scratch",
                "workflow:triage database state", "workflow:triage disk scratch",
            ],
            [.. listed.Value.Resources.Select(static r => $"{r.Owner} {r.Kind} {r.Name}")]);
        var db = listed.Value.Resources.Single(static r => r.Owner == "workflow:triage" && r.Kind == "database");
        var disk = listed.Value.Resources.Single(static r => r.Owner == "workflow:triage" && r.Kind == "disk");
        Assert.Equal(40, db.Size);
        Assert.Equal(Path.Combine(Root, "workflow", "triage", "state.db"), db.Path);
        Assert.Equal(300, disk.Size);
    }

    [Fact]
    public async Task One_owner_lists_only_its_own_and_a_bad_owner_is_refused()
    {
        Provision("workflow:triage");
        Provision("toolkit:mail");

        var one = await _service.Handle(new ListResourcesRequest("toolkit:mail"), Token);
        var bad = await _service.Handle(new ListResourcesRequest("mail"), Token);
        var none = await _service.Handle(new ListResourcesRequest("workflow:absent"), Token);

        Assert.All(one.Value.Resources, static r => Assert.Equal("toolkit:mail", r.Owner));
        Assert.Equal(2, one.Value.Resources.Count);
        Assert.Equal("SND-RES-006", bad.Error.Code);
        Assert.Empty(none.Value.Resources);
    }

    private static ListResourcesService Build() => new();

    private void Provision(string owner)
    {
        var parsed = ResourceOwner.Parse(owner).Value;
        var plan = ResourcePlanner.Plan(parsed, new ResourceSpec([new DatabaseResource("state")], [new DiskResource("scratch", "1MiB")], Sandbox: null, []), Root).Value;
        Assert.True(new ResourceStore(Root).Provision(parsed, plan).IsSuccess);
    }
}
