using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.CheckTraits;

/// <summary>trait-check reads an edited PNG '--file' as an image instead of skipping it as binary (spec 5.2).</summary>
[Lane(TestLane.Unit)]
public sealed class TraitCheckImageTests
{
    [Fact]
    public async Task TraitCheck_edited_png_file_is_sent_as_an_image()
    {
        using var repo = new TraitRepo();
        Directory.CreateDirectory(Path.Combine(repo.Root, "src"));
        var png = Path.Combine(repo.Root, "src", "logo.png");
        await File.WriteAllBytesAsync(png, ImageBytes.Png(600, 400, "v1"), TestContext.Current.CancellationToken);
        _ = repo.Git("add", "src/logo.png");
        _ = repo.Git("-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "logo");
        await File.WriteAllBytesAsync(png, ImageBytes.Png(600, 400, "v2"), TestContext.Current.CancellationToken);
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/logo.png").Build(), TestContext.Current.CancellationToken);

        var asked = Assert.Single(harness.Jev.Asked);
        var image = Assert.Single(asked.Images);
        Assert.Equal(Path.GetFullPath(png), image.Path);
        Assert.True(asked.ImagesBestEffort);
    }
}
