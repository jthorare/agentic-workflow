using System.Security.Cryptography;
using Aw.Judge.Core.Images;
using Aw.Judge.Infrastructure.Images;
using Aw.Judge.Tests.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

/// <summary>Scaling is deterministic at a pinned SkiaSharp version, so what Jev sees can't change under a package bump unnoticed. Real files and the native library: integration.</summary>
[Lane(TestLane.Integration)]
public sealed class SkiaScalerPinnedTests
{
    // The sha256 of tests/fixtures/judge/scale/blocks-2000x1600.png scaled to 1568x1254 by SkiaImageScaler (Mitchell, PNG out) under SkiaSharp
    // 3.119.4. A SkiaSharp bump that changes the resampler moves it: record the new value and say so in the PR.
    private const string PinnedSha256 = "47a6100c855c542a1f6f8c9e98efb39149ff9a5f33cf7ec96ec82552bc9121b6";

    [Fact]
    public void Scaled_bytes_match_the_pinned_sha256()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Scale", "blocks-2000x1600.png"));
        var header = ImageHeader.Read(bytes)!;   // why: the fixture is a PNG
        var image = JevImages.Attachment("/fixtures/blocks.png", bytes, header.Value.Width, header.Value.Height);

        var scaled = new SkiaImageScaler().Fit(image, JevLimits.Default);

        Assert.NotNull(scaled);
        Assert.Equal((1568, 1254), (scaled.Width, scaled.Height));
        Assert.Equal(PinnedSha256, Convert.ToHexStringLower(SHA256.HashData(scaled.Bytes.Span)));
    }
}
