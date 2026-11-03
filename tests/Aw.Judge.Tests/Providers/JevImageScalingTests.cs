using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Infrastructure.Images;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Judge.Tests.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Providers;

/// <summary>
/// Jev gets an image too big for it shrunk to fit (images spec 3.3a), through the evaluator with the default chain: what is sent, what the
/// decision row says was sent, and what happens when the image can't be fitted or the scaler's native library won't load.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class JevImageScalingTests
{
    private const string One = """{"afterScreenshot":"home.png","evidenceDir":"/ev"}""";

    [Fact]
    public async Task Image_that_cant_fit_both_limits_skips_jev()
    {
        var wide = ImageBytes.Png(4000, 500, "wide");
        var handler = RecordingJevHandler.Choosing("looks-right");
        using var jev = JevImages.Jev(handler, new FakeImageScaler());
        var cli = Cli(ContentClass.Image).Decides("looks-right", 0.9);
        var run = new QuestionRun { ImageReader = new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/ev/home.png"] = wide }) };

        var outcome = await run.Ask(VisualCritiqueQuestion.Definition, One, jev, cli);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(ProviderName.ClaudeCli, decided.Model);
        Assert.Empty(handler.Requests);
        var skip = Assert.Single(run.Log.Decisions.Single().Skipped);
        Assert.Equal(("jev", SkipReason.Unavailable, "image-out-of-limits"), (skip.Provider, skip.Reason, skip.Code));
        Assert.Equal(JevImages.Sha256(wide), Assert.Single(Assert.Single(cli.Calls).Question.Images).Sha256);   // the CLI gets the original
    }

    [Fact]
    public async Task Scaler_whose_native_library_fails_to_load_skips_the_image_and_the_call_is_answered()
    {
        var retina = ImageBytes.Png(3024, 1964, "retina");
        var warnings = new List<string>();
        var scaler = new SkiaImageScaler(() => throw new DllNotFoundException("libSkiaSharp"), warnings.Add);
        var handler = RecordingJevHandler.Choosing("looks-right");
        using var jev = JevImages.Jev(handler, scaler);
        var cli = Cli(ContentClass.Image).Decides("looks-right", 0.9);
        var run = new QuestionRun { ImageReader = new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/ev/home.png"] = retina }) };

        var first = await run.Ask(VisualCritiqueQuestion.Definition, One, jev, cli);
        var second = await run.Ask(VisualCritiqueQuestion.Definition, One, jev, cli);

        foreach (var outcome in new[] { first, second })
        {
            Assert.Equal(ProviderName.ClaudeCli, Assert.IsType<DecidedOutcome>(outcome).Model);
        }

        Assert.Empty(handler.Requests);
        _ = Assert.Single(warnings);   // one warning per process, not one per image
        Assert.All(run.Log.Decisions, row => Assert.Equal("image-out-of-limits", Assert.Single(row.Skipped).Code));
    }

    [Fact]
    public async Task Fitting_image_is_sent_byte_for_byte()
    {
        var page = ImageBytes.Png(1512, 982, "page");
        var handler = RecordingJevHandler.Choosing("looks-right");
        using var jev = JevImages.Jev(handler, new SkiaImageScaler());
        var run = new QuestionRun { ImageReader = new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/ev/home.png"] = page }) };

        var outcome = await run.Ask(VisualCritiqueQuestion.Definition, One, jev, Cli(ContentClass.Image).Decides("looks-right", 0.9));

        Assert.Equal(ProviderName.Jev, Assert.IsType<DecidedOutcome>(outcome).Model);
        var sent = Assert.Single(Assert.Single(handler.Requests).Parts, p => p.Name != "request");
        Assert.Equal(JevImages.Sha256(page), JevImages.Sha256(sent.Bytes));
    }
}
