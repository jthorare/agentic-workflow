using System.Net;
using System.Text;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>A hook's best-effort call asks Jev again as text at most once, and only with at least half the budget left (spec 2).</summary>
[Lane(TestLane.Unit)]
public sealed class JevBestEffortRetryTests
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_413_with_under_half_the_budget_left_is_not_asked_again_and_jev_errored_http_413()
    {
        var handler = new ImageStatusHandler(HttpStatusCode.RequestEntityTooLarge, delay: TimeSpan.FromMilliseconds(600));
        using var jev = JevImages.Jev(handler);

        var result = await jev.Decide(Question(), input: null, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal("http-413", Assert.IsType<ProviderErrored>(result).ReasonCode);
        _ = Assert.Single(handler.Requests);
        var notes = result.ImageNotes;
        Assert.NotNull(notes);
        Assert.Contains("not enough of the budget was left to ask jev again", Assert.Single(notes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_400_on_an_image_call_is_asked_again_once_as_text()
    {
        var handler = new ImageStatusHandler(HttpStatusCode.BadRequest);
        using var jev = JevImages.Jev(handler);

        var result = await jev.Decide(Question(), input: null, _budget, TestContext.Current.CancellationToken);

        Assert.Equal("continue", Assert.IsType<ProviderDecided>(result).Decision);
        Assert.Equal(2, handler.Requests.Count);
        Assert.NotNull(handler.Requests[0].ImagesHeader);
        Assert.Null(handler.Requests[1].ImagesHeader);
        Assert.Equal(["Jev refused the request (400); asked jev again as text"], result.ImageNotes);
    }

    [Fact]
    public async Task A_second_413_on_the_text_retry_is_not_asked_a_third_time()
    {
        var handler = new ImageStatusHandler(HttpStatusCode.RequestEntityTooLarge, textStatus: HttpStatusCode.RequestEntityTooLarge);
        using var jev = JevImages.Jev(handler);

        var result = await jev.Decide(Question(), input: null, _budget, TestContext.Current.CancellationToken);

        Assert.Equal("http-413", Assert.IsType<ProviderErrored>(result).ReasonCode);
        Assert.Equal(2, handler.Requests.Count);
    }

    // The short-budget answer for an image Jev can't take keeps its own reason code; it was never a 400 that Jev sent.
    [Fact]
    public async Task Out_of_limits_with_no_budget_left_keeps_its_own_reason_code()
    {
        var handler = new ImageStatusHandler(HttpStatusCode.OK);
        using var jev = JevImages.Jev(handler, new FitsOnceScaler());

        var result = await jev.Decide(Question(), input: null, TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.Equal(ProviderUnavailable.ImageOutOfLimits, Assert.IsType<ProviderUnavailable>(result).ReasonCode);
        Assert.Empty(handler.Requests);
    }

    private static QuestionRef Question() => new()
    {
        Name = "ask-check",
        Outputs = ["continue", "ask"],
        Prompt = "Should the agent continue?",
        ContentClass = ContentClass.Transcript,
        Images = [JevImages.Attachment("/shots/home.png", 1512, 982, "home")],
        ImagesBestEffort = true,
    };

    // The scaler says the image fits when the call is planned and that it does not when the call is made: the case the short-budget branch has to label.
    private sealed class FitsOnceScaler : IImageScaler
    {
        private int _calls;

        public ImageAttachment? Fit(ImageAttachment image, JevLimits limits) => _calls++ == 0 ? image : null;
    }

    // Jev as a call finds it: <paramref name="imageStatus"/> for a request that holds images, <paramref name="textStatus"/> (OK by default) for one that does not.
    private sealed class ImageStatusHandler(HttpStatusCode imageStatus, HttpStatusCode textStatus = HttpStatusCode.OK, TimeSpan delay = default) : HttpMessageHandler
    {
        private readonly List<RecordedJevRequest> _requests = [];

        public IReadOnlyList<RecordedJevRequest> Requests
        {
            get
            {
                lock (_requests)
                {
                    return [.. _requests];
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = await RecordedJevRequest.Capture(request, cancellationToken);
            lock (_requests)
            {
                _requests.Add(recorded);
            }

            var withImages = recorded.ImagesHeader is not null;
            if (withImages && delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            var status = withImages ? imageStatus : textStatus;
            var body = status == HttpStatusCode.OK ? JevImages.Choice("continue") : "{}";
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
