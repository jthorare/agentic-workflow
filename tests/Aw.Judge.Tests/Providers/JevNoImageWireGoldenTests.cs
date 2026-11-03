using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>
/// The rule that an old caller's Jev call is byte for byte today's (images spec, 6): the request a call without images sends is a golden
/// captured before Jev took images, so it is green now and has to stay green. The golden file is real, so the integration lane.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class JevNoImageWireGoldenTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_call_without_images_sends_todays_request_byte_for_byte(bool emptyImageList)
    {
        var handler = RecordingJevHandler.Choosing("batch");
        using var jev = JevImages.Jev(handler);
        var question = emptyImageList ? Questions.Text with { Images = [] } : Questions.Text;

        _ = await jev.Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        var wire = string.Join('\n',
            $"{request.Method} {request.Uri}",
            $"Authorization: {request.Authorization}",
            $"Content-Type: {request.ContentType}",
            $"TypeSafe-Danger-Zone-Images: {request.ImagesHeader ?? "(not sent)"}",
            "",
            request.Body) + "\n";
        Golden.Match(wire, typeof(JevNoImageWireGoldenTests).Assembly, "Providers/Golden/jev-no-image-request.txt", Normalization.None);
    }
}
