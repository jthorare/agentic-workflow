using System.Globalization;
using Aw.Judge.Core.Batching;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.CheckTraits;

/// <summary>An edited image whose picture was not sent is skipped as 'image-not-sent', never judged on its name alone (spec 3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class TraitCheckImageSkippedTests
{
    private const string Dropped = "image 1 is 200x120, under Jev's 280 px minimum; asked jev as text";

    [Fact]
    public async Task TraitCheck_edited_png_dropped_by_jev_is_skipped_as_image_not_sent_and_its_answer_unused()
    {
        using var repo = new TraitRepo();
        EditedPngs(repo, "logo");
        var jev = new FakeBatchClassifier(request => new BatchDecided(Violations(request)) { ImageNotes = [Dropped] });
        using var harness = new CheckTraitsHarness(repo, jev);

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/logo.png").Build(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Findings);
        Assert.False(result.Value.JevUnavailable);
        Assert.Equal("image-not-sent", result.Value.Skipped["src/logo.png"]);
        Assert.Contains(harness.Log.Entries, entry => entry["event"]?.GetValue<string>() == "image-not-sent" && entry["reason"]?.GetValue<string>() == Dropped);
    }

    [Fact]
    public async Task TraitCheck_edited_images_past_four_files_are_skipped_as_image_not_sent()
    {
        using var repo = new TraitRepo();
        var names = Enumerable.Range(1, 5).Select(n => "p" + n.ToString(CultureInfo.InvariantCulture)).ToArray();
        EditedPngs(repo, names);
        using var harness = new CheckTraitsHarness(repo, new FakeBatchClassifier(request => new BatchDecided(Violations(request))));

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit([.. names.Select(n => $"src/{n}.png")]).Build(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var skipped = Assert.Single(result.Value.Skipped);
        Assert.Equal(("src/p5.png", "image-not-sent"), (skipped.Key, skipped.Value));
        Assert.Equal(4, harness.Jev.Asked.Count(request => request.Images.Count == 1));
    }

    private static Dictionary<string, BatchAnswer> Violations(BatchRequest request) => request.Questions.ToDictionary(
        question => question.Key,
        question => (BatchAnswer)new ChoiceBatchAnswer("violation", 0.9, new Dictionary<string, double>(StringComparer.Ordinal) { ["violation"] = 0.9 }),
        StringComparer.Ordinal);

    // Commits each PNG, then rewrites it, so the check sees an edited image file.
    private static void EditedPngs(TraitRepo repo, params string[] names)
    {
        foreach (var name in names)
        {
            File.WriteAllBytes(Path.Combine(repo.Root, "src", name + ".png"), ImageBytes.Png(600, 400, "v1"));
        }

        _ = repo.Git("add", "-A");
        _ = repo.Git("-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "images");
        foreach (var name in names)
        {
            File.WriteAllBytes(Path.Combine(repo.Root, "src", name + ".png"), ImageBytes.Png(600, 400, "v2"));
        }
    }
}
