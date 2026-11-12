using Aw.Kernel.Testing;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Resources;

/// <summary>The listing and the purge count on the files under the resources root.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ResourceFilesTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private string Root => Path.Combine(_home.Path, "resources");

    [Fact]
    public void A_disk_too_big_to_measure_is_listed_as_unmeasured_not_as_size_zero()
    {
        var disk = Path.Combine(Root, "workflow", "triage", "scratch");
        _ = Directory.CreateDirectory(disk);
        for (var i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(disk, $"f{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}.txt"), "x");
        }

        var listed = new ResourceFiles(Root, entryCap: 3).List(owner: null).Value;

        var entry = Assert.Single(listed);
        Assert.True(entry.Unmeasured);
    }

    [Fact]
    public void A_disk_that_measures_is_not_marked_unmeasured()
    {
        var disk = Path.Combine(Root, "workflow", "triage", "scratch");
        _ = Directory.CreateDirectory(disk);
        File.WriteAllText(Path.Combine(disk, "f.txt"), "x");

        var entry = Assert.Single(new ResourceFiles(Root).List(owner: null).Value);

        Assert.False(entry.Unmeasured);
        Assert.Equal(1, entry.SizeBytes);
    }

    [Fact]
    public void Purge_counts_a_stray_file_next_to_the_databases_and_disks()
    {
        var folder = Path.Combine(Root, "workflow", "triage");
        _ = Directory.CreateDirectory(Path.Combine(folder, "scratch"));
        File.WriteAllText(Path.Combine(folder, "state.db"), "db");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "stray");

        var removed = new ResourceFiles(Root).Purge(ResourceOwner.Parse("workflow:triage").Value);

        Assert.Equal(3, removed.Value);   // the disk, the database and the stray file
        Assert.False(Directory.Exists(folder));
    }
}
