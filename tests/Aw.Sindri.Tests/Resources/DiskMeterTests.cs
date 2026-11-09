using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Resources;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DiskMeterTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "unit")]
    public void An_over_size_disk_fails_the_run_with_used_and_limit_numbers()
    {
        var folder = Path.Combine(_home.Path, "disk");
        _ = Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "big.bin"), new byte[6 << 20]);

        var result = DiskMeter.Check("scratch", folder, 5L << 20);

        Assert.Equal("SND-RES-001", result.Error.Code);
        Assert.Contains("6291456", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("5242880", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("scratch", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_disk_within_its_limit_reports_the_bytes_used_including_subfolders()
    {
        var folder = Path.Combine(_home.Path, "disk");
        _ = Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.WriteAllBytes(Path.Combine(folder, "a.bin"), new byte[1000]);
        File.WriteAllBytes(Path.Combine(folder, "sub", "b.bin"), new byte[500]);

        Assert.Equal(1500, DiskMeter.Check("scratch", folder, 1 << 20).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_symlink_pointing_outside_the_folder_is_not_followed_or_counted()
    {
        var outside = Path.Combine(_home.Path, "outside");
        _ = Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "huge.bin"), new byte[6 << 20]);
        var folder = Path.Combine(_home.Path, "disk");
        _ = Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "a.bin"), new byte[100]);
        _ = File.CreateSymbolicLink(Path.Combine(folder, "dir-link"), outside);
        _ = File.CreateSymbolicLink(Path.Combine(folder, "file-link"), Path.Combine(outside, "huge.bin"));

        var result = DiskMeter.Check("scratch", folder, 1 << 20);

        Assert.Equal(100, result.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_walk_the_cap_stops_under_the_limit_fails_with_the_count_and_the_bytes_so_far()
    {
        var folder = Path.Combine(_home.Path, "disk");
        _ = Directory.CreateDirectory(Path.Combine(folder, "a", "b"));   // folders count toward the cap too
        for (var i = 0; i < 4; i++)
        {
            File.WriteAllBytes(Path.Combine(folder, $"f{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}.bin"), new byte[10]);
        }

        var result = DiskMeter.Check("scratch", folder, 1 << 20, entryCap: 5);

        Assert.Equal("SND-RES-007", result.Error.Code);
        Assert.Contains("more than 5 files and folders", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("scratch", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(40, DiskMeter.Check("scratch", folder, 1 << 20, entryCap: 6).Value);   // a, a/b and the four files: six entries fit a cap of six
    }
}
