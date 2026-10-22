using Aw.Kernel.Testing;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Prove;
using Aw.Sindri.Testing.Prove;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Prove;

/// <summary>The disk holdout store honours the holdout store contract, and keeps the store owner-only.</summary>
[Lane(TestLane.Integration)]
public sealed class DiskHoldoutStoreContractTests : HoldoutStoreContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override IHoldoutStore Create() => new DiskHoldoutStore(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")));

    protected override void Seed(IHoldoutStore store, string line, string phase, string? setText, string? sealText)
    {
        var dir = ((DiskHoldoutStore)store).Directory(line, phase);
        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(dir);
        }
        else
        {
            _ = Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);   // the author Step makes it owner-only; the store leaves an existing directory as it is
        }
        if (setText is not null)
        {
            File.WriteAllText(Path.Combine(dir, HoldoutFiles.SetName), setText);
        }

        if (sealText is not null)
        {
            File.WriteAllText(Path.Combine(dir, HoldoutFiles.SealName), sealText);
        }
    }

    [Fact]
    public async Task Marking_spent_writes_an_owner_only_file_in_an_owner_only_directory()
    {
        var store = Create();
        Seed(store, "T-7", "p1", """{ "phase": "p1", "scenarios": [ { "id": "a", "kind": "api" } ] }""", sealText: null);

        _ = await store.MarkSpent("T-7", "p1", ["a"], TestContext.Current.CancellationToken);

        var dir = ((DiskHoldoutStore)store).Directory("T-7", "p1");
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(dir, HoldoutFiles.SpentName)));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(dir));
        }
    }
}
