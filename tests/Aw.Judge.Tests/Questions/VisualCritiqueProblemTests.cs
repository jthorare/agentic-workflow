using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

/// <summary>An image that can't be read escalates, and the row says why, so a missing screenshot can be told from an escaped one.</summary>
[Lane(TestLane.Unit)]
public sealed class VisualCritiqueProblemTests
{
    private const string Input = """{"afterScreenshot":"after.png","evidenceDir":"/tmp/ev"}""";

    [Fact]
    public async Task A_missing_screenshot_is_stored_as_the_reason_on_the_escalated_row()
    {
        var run = new QuestionRun();

        _ = await run.Ask(VisualCritiqueQuestion.Definition, Input, Cli(ContentClass.Image).Decides("looks-right", 1));

        var reasons = Assert.Single(run.Log.Decisions).Details?.Reasons;
        Assert.Contains("does not exist", Assert.Single(reasons ?? []), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_screenshot_that_escapes_the_evidence_dir_is_stored_as_outside()
    {
        var reader = new FakeImageReader(
            new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/tmp/secret/x.png"] = Aw.Judge.Tests.Images.ImageBytes.Png(1, 1) },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["/tmp/ev/after.png"] = "/tmp/secret/x.png" });
        var run = new QuestionRun { ImageReader = reader };

        _ = await run.Ask(VisualCritiqueQuestion.Definition, Input, Cli(ContentClass.Image).Decides("looks-right", 1));

        var reasons = Assert.Single(run.Log.Decisions).Details?.Reasons;
        Assert.Contains("outside", Assert.Single(reasons ?? []), StringComparison.Ordinal);
    }
}
