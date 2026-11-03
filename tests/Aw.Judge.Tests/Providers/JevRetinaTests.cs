using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Host.Commands;
using Aw.Judge.Infrastructure.Images;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Judge.Tests.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Providers;

/// <summary>A retina frame goes to Jev shrunk by the real scaler over the native SkiaSharp library, so the integration lane.</summary>
[Lane(TestLane.Integration)]
public sealed class JevRetinaTests
{
    [Fact]
    public async Task Retina_frame_is_sent_to_jev_as_1568x1018()
    {
        var retina = SkiaImages.Encode(3024, 1964, ImageFormat.Png);
        var handler = RecordingJevHandler.Choosing("looks-right");
        using var jev = JevImages.Jev(handler, new SkiaImageScaler());
        var cli = Cli(ContentClass.Image).Decides("looks-right", 0.9);
        var run = new QuestionRun { ImageReader = new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/ev/home.png"] = retina }) };

        var outcome = await run.Ask(VisualCritiqueQuestion.Definition, """{"afterScreenshot":"home.png","evidenceDir":"/ev"}""", jev, cli);

        Assert.Equal(ProviderName.Jev, Assert.IsType<DecidedOutcome>(outcome).Model);
        Assert.Empty(cli.Calls);
        var part = Assert.Single(Assert.Single(handler.Requests).Parts, p => p.Name != "request");
        Assert.Matches("^aw-image-1-[0-9a-f]{12}$", part.Name);
        Assert.Equal("image/png", part.ContentType);
        var sent = ImageHeader.Read(part.Bytes);
        Assert.Equal((ImageFormat.Png, 1568, 1018), sent);
        var notes = DecisionOutput.From(run.Log.Decisions.Single()).Notes;
        Assert.Contains(notes ?? [], note => note.EndsWith("-> sent to jev as 1568x1018", StringComparison.Ordinal));
    }
}
