using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>
/// The rule that an old caller's batched Jev call is byte for byte today's (images spec, 3.6 and 6): the request a batch without images sends, in the
/// prompt-sort and trait-check shapes, is a golden captured on main before batches took images. The golden files are real, so the integration lane.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class JevBatchNoImageWireGoldenTests
{
    [Theory]
    [InlineData("prompt-sort", false)]
    [InlineData("prompt-sort", true)]
    [InlineData("trait-check", false)]
    [InlineData("trait-check", true)]
    public async Task A_batch_without_images_sends_todays_request_byte_for_byte(string shape, bool emptyImageList)
    {
        var handler = RecordingJevHandler.Choosing("batch");
        using var jev = JevImages.Jev(handler);
        var batch = shape == "prompt-sort" ? PromptSort() : TraitCheck();
        batch = emptyImageList ? batch with { Images = [] } : batch;

        _ = await jev.Batch(batch, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        var wire = string.Join('\n',
            $"{request.Method} {request.Uri}",
            $"Authorization: {request.Authorization}",
            $"Content-Type: {request.ContentType}",
            $"TypeSafe-Danger-Zone-Images: {request.ImagesHeader ?? "(not sent)"}",
            "",
            request.Body) + "\n";
        Golden.Match(wire, typeof(JevBatchNoImageWireGoldenTests).Assembly, $"Providers/Golden/jev-batch-no-image-request-{shape}.txt", Normalization.None);
    }

    private static BatchRequest PromptSort() => new(
        new JsonObject { ["prompt"] = "fix the login button" },
        [
            new ChoiceBatchQuestion("ambiguity", "How clear is the request?", new Dictionary<string, string>(StringComparer.Ordinal) { ["clear"] = "it is", ["vague"] = "it is not" }),
            new NoulBatchQuestion("touchesUi", "Does the work change the UI?"),
            new ScoreBatchQuestion("complexity", "How much work is it?", ["trivial", "small", "large"]),
        ],
        TimeSpan.FromSeconds(5));

    private static BatchRequest TraitCheck() => new(
        new JsonObject
        {
            ["mode"] = "edit",
            ["source"] = new JsonObject { ["path"] = "src/app.ts", ["startLine"] = 1, ["endLine"] = 3, ["text"] = "const a = 1;\n// TODO: remove\nexport { a };" },
        },
        [new NoulBatchQuestion("no-todo", "Does this code leave a TODO comment behind?")],
        TimeSpan.FromSeconds(5));
}
