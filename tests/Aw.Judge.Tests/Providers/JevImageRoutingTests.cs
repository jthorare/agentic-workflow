using System.Text.Json.Nodes;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Judge.Tests.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Providers;

/// <summary>
/// Jev takes a call's images ahead of the agent CLIs, through the evaluator with the default chain (jev, then claude-cli, then rules): which
/// questions it settles, what it sends and why it is passed over. Jev runs over a recording handler, the CLI is scripted, the images are in memory.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class JevImageRoutingTests
{
    private const string Looks = """{"afterScreenshot":"home.png","baselineScreenshot":"base/home.png","evidenceDir":"/ev"}""";

    private const string ResolutionInput = """
        {"brief":"Saving a draft loses the title","expected":"title kept","actual":"title lost","rootCause":"stale form state","checkKind":"test","checkSummary":"draft round trip","beforePassed":false,"afterPassed":true,"diffStat":"1 file changed"}
        """;

    [Fact]
    public async Task VisualCritique_looks_right_is_settled_by_jev_from_multipart_screenshots()
    {
        var after = ImageBytes.Png(1512, 982, "after");
        var before = ImageBytes.Png(1512, 982, "before");
        var handler = RecordingJevHandler.Choosing("looks-right");
        using var jev = JevImages.Jev(handler);
        var cli = Cli(ContentClass.Image).Decides("looks-right", 0.9);
        var run = new QuestionRun { ImageReader = new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/ev/home.png"] = after, ["/ev/base/home.png"] = before }) };

        var outcome = await run.Ask(VisualCritiqueQuestion.Definition, Looks, jev, cli);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("looks-right", ProviderName.Jev), (decided.Decision, decided.Model));
        Assert.Empty(cli.Calls);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("true", request.ImagesHeader);
        Assert.Equal("multipart/form-data", request.ContentType);
        var afterRef = JevImages.Reference(1, after);
        var beforeRef = JevImages.Reference(2, before);
        Assert.Equal(["request", afterRef, beforeRef], request.Parts.Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(["application/json", "image/png", "image/png"], request.Parts.Select(p => p.ContentType), StringComparer.Ordinal);
        Assert.Equal([afterRef, beforeRef], request.Parts.Skip(1).Select(p => p.FileName), StringComparer.Ordinal);
        Assert.Equal(after, request.Parts[1].Bytes);
        Assert.Equal(before, request.Parts[2].Bytes);
        var body = request.Json;
        Assert.Equal($$"""{"type":"image","part":"{{afterRef}}"}""", body["assets"]?[afterRef]?.ToJsonString());
        Assert.Equal($$"""{"type":"image","part":"{{beforeRef}}"}""", body["assets"]?[beforeRef]?.ToJsonString());
        Assert.Equal(2, body["assets"]?.AsObject().Count);
        Assert.Equal(afterRef, body["state"]?["afterScreenshot"]?.GetValue<string>());
        Assert.Equal(beforeRef, body["state"]?["baselineScreenshot"]?.GetValue<string>());
    }

    [Fact]
    public async Task VisualCritique_looks_off_from_jev_goes_to_the_cli_for_reasons()
    {
        var handler = RecordingJevHandler.Choosing("looks-off");
        using var jev = JevImages.Jev(handler);
        var reasons = new JsonObject { ["reasons"] = new JsonArray("the nav overlaps the title") };
        var cli = Cli(ContentClass.Image).Returns(new ProviderDecided("looks-off", 0.9, "model") { Extra = reasons });
        var run = new QuestionRun { ImageReader = new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/ev/home.png"] = ImageBytes.Png(1512, 982, "a"), ["/ev/base/home.png"] = ImageBytes.Png(1512, 982, "b") }) };

        var outcome = await run.Ask(VisualCritiqueQuestion.Definition, Looks, jev, cli);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("looks-off", ProviderName.ClaudeCli), (decided.Decision, decided.Model));
        _ = Assert.Single(handler.Requests);
        _ = Assert.Single(cli.Calls);
        Assert.Empty(run.Log.Failures);
        var skip = Assert.Single(run.Log.Decisions.Single().Skipped);
        Assert.Equal(("jev", "no-reasons"), (skip.Provider, skip.Code));
        Assert.NotEqual(SkipReason.Failed, skip.Reason);
    }

    [Fact]
    public async Task ResolutionCheck_still_refuses_jev()
    {
        var handler = RecordingJevHandler.Choosing("resolved");
        using var jev = JevImages.Jev(handler);
        var cli = Cli(ContentClass.Brief).Decides("resolved", 0.95);
        var run = new QuestionRun();

        var outcome = await run.Ask(ResolutionCheckQuestion.Definition, ResolutionInput, jev, cli);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("resolved", ProviderName.ClaudeCli), (decided.Decision, decided.Model));
        Assert.Empty(handler.Requests);
        var skip = Assert.Single(run.Log.Decisions.Single().Skipped);
        Assert.Equal(("jev", SkipReason.Unavailable), (skip.Provider, skip.Reason));
    }

    [Fact]
    public async Task Image_under_280px_skips_jev_for_the_clis()
    {
        var icon = ImageBytes.Png(200, 120, "icon");
        var handler = RecordingJevHandler.Choosing("looks-right");
        using var jev = JevImages.Jev(handler);
        var cli = Cli(ContentClass.Image).Decides("looks-right", 0.9);
        var run = new QuestionRun { ImageReader = new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/ev/home.png"] = icon }) };

        var outcome = await run.Ask(VisualCritiqueQuestion.Definition, """{"afterScreenshot":"home.png","evidenceDir":"/ev"}""", jev, cli);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("looks-right", ProviderName.ClaudeCli), (decided.Decision, decided.Model));
        Assert.Empty(handler.Requests);
        var skip = Assert.Single(run.Log.Decisions.Single().Skipped);
        Assert.Equal(("jev", SkipReason.Unavailable, "image-out-of-limits"), (skip.Provider, skip.Reason, skip.Code));
        Assert.Equal(JevImages.Sha256(icon), Assert.Single(Assert.Single(cli.Calls).Question.Images).Sha256);
    }
}
