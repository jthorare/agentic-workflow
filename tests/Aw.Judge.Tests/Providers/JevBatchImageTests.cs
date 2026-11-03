using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Images;
using Aw.Judge.Tests.Images;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>The batched Jev call with images (spec 3.6): one multipart request, a drop recorded on the result and never printed, a 413 retried as text.</summary>
[Lane(TestLane.Unit)]
public sealed class JevBatchImageTests
{
    private const string Answer = """{"answers":{"touchesUi":{"type":"noul","noul":0.9}},"usage":{"input_tokens":12,"output_tokens":1}}""";

    private static readonly NoulBatchQuestion _noul = new("touchesUi", "Does the work change the UI?");

    [Fact]
    public async Task Batch_with_an_image_sends_one_multipart_request_with_the_reference_once_in_the_state()
    {
        var bytes = ImageBytesOf(1512, 982, "home");
        var handler = new RecordingJevHandler(_ => Answer);
        using var jev = JevImages.Jev(handler);
        var batch = Batch([JevImages.Attachment("/ev/home.png", bytes, 1512, 982)]);

        var result = await jev.Batch(batch, TestContext.Current.CancellationToken);

        _ = Assert.IsType<BatchDecided>(result);
        var request = Assert.Single(handler.Requests);
        var reference = JevImages.Reference(1, bytes);
        Assert.Equal(("true", "multipart/form-data"), (request.ImagesHeader, request.ContentType));
        Assert.Equal(["request", reference], request.Parts.Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(bytes, request.Parts[1].Bytes);
        var body = request.Json;
        Assert.Equal($$"""{"type":"image","part":"{{reference}}"}""", body["assets"]?[reference]?.ToJsonString());
        Assert.Equal([reference], body["state"]?["images"]?.AsArray().Select(node => node?.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Batch_drops_an_image_under_the_minimum_and_notes_it_on_the_result_as_text()
    {
        var handler = new RecordingJevHandler(_ => Answer);
        using var jev = JevImages.Jev(handler);
        var batch = Batch([JevImages.Attachment("/ev/icon.png", 200, 120)]) with { ImagesBestEffort = true };

        var result = await jev.Batch(batch, TestContext.Current.CancellationToken);

        _ = Assert.IsType<BatchDecided>(result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(("application/json", null), (request.ContentType, request.ImagesHeader));
        Assert.Equal("image 1 is 200x120, under Jev's 280 px minimum; asked jev as text", Assert.Single(result.ImageNotes ?? []));
    }

    [Fact]
    public async Task Batch_413_on_images_asks_again_as_text_when_half_the_budget_is_left()
    {
        var statuses = new Queue<HttpStatusCode>([HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.OK]);
        var requests = new List<RecordedJevRequest>();
        using var jev = JevImages.Jev(new SequenceHandler(statuses, requests));
        var batch = Batch([JevImages.Attachment("/ev/home.png", 1512, 982)]) with { ImagesBestEffort = true };

        var result = await jev.Batch(batch, TestContext.Current.CancellationToken);

        _ = Assert.IsType<BatchDecided>(result);
        Assert.Equal(["multipart/form-data", "application/json"], requests.Select(r => r.ContentType), StringComparer.Ordinal);
        Assert.Equal("Jev said the images were too large (413); asked jev again as text", Assert.Single(result.ImageNotes ?? []));
    }

    private static byte[] ImageBytesOf(int width, int height, string tag) => ImageBytes.Png(width, height, tag);

    private static BatchRequest Batch(IReadOnlyList<ImageAttachment> images) =>
        new(new JsonObject { ["prompt"] = "see the screenshot" }, [_noul], TimeSpan.FromSeconds(10)) { Images = images };

    private sealed class SequenceHandler(Queue<HttpStatusCode> statuses, List<RecordedJevRequest> requests) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(await RecordedJevRequest.Capture(request, cancellationToken));
            return new HttpResponseMessage(statuses.Dequeue()) { Content = new StringContent(Answer, Encoding.UTF8, "application/json") };
        }
    }
}
