using Aw.Kernel.Testing;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Sandbox.Egress;

namespace Aw.Sindri.Tests.Sandbox.Scope;

/// <summary>The limits file 'session wrap --limits' reads (sandbox spec §3.3; SB-3a).</summary>
[Lane(TestLane.Unit)]
public sealed class WrapLimitsTests
{
    [Fact]
    public void A_limits_file_round_trips()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "limits.json");
        File.WriteAllText(path, """{"unit":"sindri-LCH-1.scope","wallSeconds":1800,"wallGraceSeconds":600,"parkMaxSeconds":86400,"memoryMaxBytes":8589934592}""");

        var read = WrapLimits.Read(path);

        Assert.True(read.IsSuccess);
        Assert.Equal(new WrapLimits("sindri-LCH-1.scope", 600, 86400, 8589934592, 1800), read.Value);
    }

    [Fact]
    public void An_attended_file_has_no_wall()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "limits.json");
        File.WriteAllText(path, """{"unit":"u.scope","wallGraceSeconds":600,"parkMaxSeconds":86400,"memoryMaxBytes":1}""");

        Assert.Null(WrapLimits.Read(path).Value.WallSeconds);
    }

    [Fact]
    public void A_limits_file_without_a_unit_is_refused()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "limits.json");
        File.WriteAllText(path, """{"unit":"","wallGraceSeconds":600,"parkMaxSeconds":86400,"memoryMaxBytes":1}""");

        var read = WrapLimits.Read(path);

        Assert.False(read.IsSuccess);
        Assert.Equal("SND-REQ-001", read.Error.Code);
    }

    [Fact]
    public void An_unreadable_limits_file_is_refused()
    {
        using var temp = new TempDirectory();

        var missing = WrapLimits.Read(Path.Combine(temp.Path, "none.json"));
        var path = Path.Combine(temp.Path, "bad.json");
        File.WriteAllText(path, "{not json");
        var malformed = WrapLimits.Read(path);

        Assert.False(missing.IsSuccess);
        Assert.False(malformed.IsSuccess);
    }
}
