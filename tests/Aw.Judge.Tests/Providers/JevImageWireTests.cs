using System.Net;
using Aw.Judge.Core.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>What Jev answers to a call that carries images, read as the provider's result.</summary>
[Lane(TestLane.Unit)]
public sealed class JevImageWireTests
{
    [Fact]
    public async Task Jev_413_on_images_skips_with_image_too_large()
    {
        var handler = new RecordingJevHandler(_ => "{}", HttpStatusCode.RequestEntityTooLarge);
        using var jev = JevImages.Jev(handler);
        var question = Questions.Text with { Images = [JevImages.Attachment("/ev/home.png", 1512, 982)] };

        var result = await jev.Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderUnavailable("image-too-large"), result);
        _ = Assert.Single(handler.Requests);
    }
}
