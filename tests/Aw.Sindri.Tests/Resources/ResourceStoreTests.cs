using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Resources;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ResourceStoreTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private string Root => Path.Combine(_home.Path, "resources");

    private ResourcePlan Plan() => ResourcePlanner.Plan(
        ResourceOwner.Parse("workflow:triage").Value,
        new ResourceSpec([new DatabaseResource("state")], [new DiskResource("scratch", "1MiB")], Sandbox: null, []),
        Root).Value;

    [Fact, Trait("Lane", "unit")]
    public void Provision_creates_owner_only_files_and_is_idempotent()
    {
        var owner = ResourceOwner.Parse("workflow:triage").Value;
        var store = new ResourceStore(Root);
        var plan = Plan();

        Assert.True(store.Provision(owner, plan).IsSuccess);
        var db = Path.Combine(Root, "workflow", "triage", "state.db");
        var disk = Path.Combine(Root, "workflow", "triage", "scratch");
        File.WriteAllText(Path.Combine(disk, "keep.txt"), "kept");
        File.WriteAllText(db, "rows");
        var dbTime = File.GetLastWriteTimeUtc(db);

        Assert.True(store.Provision(owner, plan).IsSuccess);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(disk));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.Combine(Root, "workflow", "triage")));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.Combine(Root, "workflow")));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(db));
        Assert.Equal("rows", File.ReadAllText(db));
        Assert.Equal(dbTime, File.GetLastWriteTimeUtc(db));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(disk, "keep.txt")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Provision_refuses_a_path_outside_the_owner_folder()
    {
        var owner = ResourceOwner.Parse("workflow:other").Value;

        var result = new ResourceStore(Root).Provision(owner, Plan());

        Assert.Equal("SND-RES-004", result.Error.Code);
        Assert.False(Directory.Exists(Path.Combine(Root, "workflow", "triage")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Provision_tightens_a_database_and_a_folder_someone_widened()
    {
        var owner = ResourceOwner.Parse("workflow:triage").Value;
        var store = new ResourceStore(Root);
        Assert.True(store.Provision(owner, Plan()).IsSuccess);
        var db = Path.Combine(Root, "workflow", "triage", "state.db");
        var disk = Path.Combine(Root, "workflow", "triage", "scratch");
        File.SetUnixFileMode(db, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        File.SetUnixFileMode(disk, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        Assert.True(store.Provision(owner, Plan()).IsSuccess);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(db));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(disk));
    }

    [Fact, Trait("Lane", "unit")]
    public void Provision_refuses_a_disk_folder_that_is_a_symlink()
    {
        var outside = Path.Combine(_home.Path, "outside");
        _ = Directory.CreateDirectory(outside);
        _ = Directory.CreateDirectory(Path.Combine(Root, "workflow", "triage"));
        _ = Directory.CreateSymbolicLink(Path.Combine(Root, "workflow", "triage", "scratch"), outside);

        var result = new ResourceStore(Root).Provision(ResourceOwner.Parse("workflow:triage").Value, Plan());

        Assert.Equal("SND-RES-004", result.Error.Code);
        Assert.Contains("symlink", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Provision_answers_an_error_for_a_path_of_the_wrong_type_instead_of_throwing()
    {
        var owner = ResourceOwner.Parse("workflow:triage").Value;
        var folder = Path.Combine(Root, "workflow", "triage");
        _ = Directory.CreateDirectory(Path.Combine(folder, "state.db"));   // a folder where the database goes
        File.WriteAllText(Path.Combine(folder, "scratch"), "a file where the disk goes");

        var result = new ResourceStore(Root).Provision(owner, Plan());

        Assert.Equal("SND-RES-006", result.Error.Code);
    }
}
