using System.Text.Json.Nodes;
using Aw.Judge.Features.EvaluateQuestion;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Judge.Tests.Providers;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.EvaluateQuestion;

/// <summary>
/// 'judge eval' re-reads each image a stored input names (spec 4): a file that is gone or whose sha256 changed makes that case skipped as
/// 'image-changed', not a failure and not a call to the provider; a file that still matches goes to the provider with its bytes.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class EvalImageChangedTests
{
    private const string RecordPath = "/tmp/images.jsonl";

    [Fact]
    public async Task Eval_replay_skips_a_case_whose_image_changed_as_image_changed()
    {
        var kept = ImageBytes.Png(1512, 982, "kept");
        var world = new EvalWorld();
        _ = world.Store
            .Label(EvalWorld.Question, "changed", Stored("/shots/changed.png", JevImages.Sha256(ImageBytes.Png(1512, 982, "before")), 1512, 982), "ask")
            .Label(EvalWorld.Question, "gone", Stored("/shots/gone.png", JevImages.Sha256(ImageBytes.Png(1512, 982, "gone")), 1512, 982), "ask")
            .Label(EvalWorld.Question, "same", Stored("/shots/same.png", JevImages.Sha256(kept), 1512, 982), "ask");
        var reader = new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["/shots/changed.png"] = ImageBytes.Png(1512, 982, "after"),
            ["/shots/same.png"] = kept,
        });
        var service = new EvaluateQuestionService(
            world.Store, new InMemoryJudgeConfigStore(), new FakeProviderCatalog(world.Jev), world.Files, new EvalRedaction(new FakeInputRedactor(), AwJson.CreateOptions(JudgeJsonContext.Default)), world.Clock, new EvalImageSource(reader, world.Workspaces));
        _ = world.Jev.Decides("ask", 0.9);

        var result = await service.Handle(A.EvaluateQuestionRequest().Record(RecordPath).Build(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : "");
        var asked = Assert.Single(world.Jev.Calls);
        var image = Assert.Single(asked.Question.Images);
        Assert.Equal(("/shots/same.png", JevImages.Sha256(kept)), (image.Path, image.Sha256));
        var lines = world.Files.Text(RecordPath)!.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonNode.Parse(line)!.AsObject()).ToDictionary(line => line["itemId"]!.GetValue<string>(), StringComparer.Ordinal);
        foreach (var skipped in new[] { "changed", "gone" })
        {
            Assert.Equal(("unavailable", "image-changed"), (lines[skipped]["status"]?.GetValue<string>(), lines[skipped]["reasonCode"]?.GetValue<string>()));
        }

        Assert.Equal("decided", lines["same"]["status"]?.GetValue<string>());
    }

    // What a decision row stores for an input with one image (spec 4): the read form, never the bytes.
    private static string Stored(string path, string sha256, int width, int height) =>
        new JsonObject
        {
            ["transcriptTail"] = "run the tests",
            ["images"] = new JsonArray(new JsonObject { ["path"] = path, ["sha256"] = sha256, ["bytes"] = 100, ["width"] = width, ["height"] = height }),
        }.ToJsonString();
}
