using System.Text.Json.Nodes;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.Questions;

/// <summary>An 'images' field that is not a list of strings: a best-effort call drops it and asks as text (a bad attachment never costs a hook its answer, spec 2), a strict one is refused.</summary>
[Lane(TestLane.Unit)]
public sealed class MalformedImagesTests
{
    private const string Cause = "images must be a list of absolute file paths";

    [Theory]
    [InlineData("\"x\"")]
    [InlineData("[1]")]
    public async Task Best_effort_drops_a_malformed_images_field_and_asks_as_text(string images)
    {
        var run = new QuestionRun();
        var jev = new ScriptedProvider(ProviderName.Jev, [.. ContentClasses.All]).Decides("continue", 0.9);

        var outcome = await run.Ask(AskCheckQuestion.Definition, Input(images, bestEffort: true), jev);

        Assert.Equal("continue", Assert.IsType<DecidedOutcome>(outcome).Decision);
        Assert.Empty(Assert.Single(jev.Calls).Question.Images);
        Assert.Equal([$"judge: {Cause}; asked as text"], outcome.StderrLines);
        var stored = JsonNode.Parse(Assert.Single(run.Log.Decisions).Details!.InputJson)!.AsObject();
        Assert.Equal($"{Cause}; asked as text", Assert.Single(stored["imagesDropped"]!.AsArray())!.GetValue<string>());
        Assert.Null(stored["images"]);
    }

    [Theory]
    [InlineData("\"x\"")]
    [InlineData("[1]")]
    public async Task Strict_refuses_a_malformed_images_field(string images)
    {
        var run = new QuestionRun();
        var jev = new ScriptedProvider(ProviderName.Jev, [.. ContentClasses.All]).Decides("continue", 0.9);

        var outcome = await run.Ask(AskCheckQuestion.Definition, Input(images, bestEffort: false), jev);

        Assert.Equal("invalid-images", Assert.IsType<EscalatedOutcome>(outcome).ReasonCode);
        Assert.Equal([$"judge: {Cause}"], outcome.StderrLines);
        Assert.Empty(jev.Calls);
    }

    private static string Input(string images, bool bestEffort) =>
        $$"""{"transcriptTail":"run the tests","images":{{images}}{{(bestEffort ? ",\"imagesBestEffort\":true" : "")}}}""";
}
