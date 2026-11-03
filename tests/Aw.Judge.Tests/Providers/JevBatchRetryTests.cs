using System.Net;
using System.Text;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>What a batch with images does after Jev refuses it (spec 3.6): one text retry when at least half the budget is left, never a third request.</summary>
[Lane(TestLane.Unit)]
public sealed class JevBatchRetryTests
{
    private const string Answer = """{"answers":{"touchesUi":{"type":"noul","noul":0.9}}}""";

    private static readonly NoulBatchQuestion _noul = new("touchesUi", "Does the work change the UI?");

    [Fact]
    public async Task A_413_with_under_half_the_budget_left_asks_no_second_time_and_errors_http_413()
    {
        var handler = new Scripted([(HttpStatusCode.RequestEntityTooLarge, TimeSpan.FromMilliseconds(700)), (HttpStatusCode.OK, TimeSpan.Zero)]);
        using var jev = JevImages.Jev(handler);

        var result = await jev.Batch(Batch(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        Assert.Equal("http-413", Assert.IsType<ProviderErrored>(result).ReasonCode);
        _ = Assert.Single(handler.Requests);
        Assert.Contains("not enough of the budget was left", Assert.Single(result.ImageNotes ?? []), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_400_on_an_image_batch_asks_once_more_as_text()
    {
        var handler = new Scripted([(HttpStatusCode.BadRequest, TimeSpan.Zero), (HttpStatusCode.OK, TimeSpan.Zero)]);
        using var jev = JevImages.Jev(handler);

        var result = await jev.Batch(Batch(TimeSpan.FromSeconds(10)), TestContext.Current.CancellationToken);

        _ = Assert.IsType<BatchDecided>(result);
        Assert.Equal(["multipart/form-data", "application/json"], handler.Requests.Select(r => r.ContentType), StringComparer.Ordinal);
        Assert.Equal("Jev refused the request (400); asked jev again as text", Assert.Single(result.ImageNotes ?? []));
    }

    [Fact]
    public async Task A_second_413_on_the_text_retry_is_the_end_no_third_request()
    {
        var handler = new Scripted([(HttpStatusCode.RequestEntityTooLarge, TimeSpan.Zero), (HttpStatusCode.RequestEntityTooLarge, TimeSpan.Zero), (HttpStatusCode.OK, TimeSpan.Zero)]);
        using var jev = JevImages.Jev(handler);

        var result = await jev.Batch(Batch(TimeSpan.FromSeconds(10)), TestContext.Current.CancellationToken);

        Assert.Equal("http-413", Assert.IsType<ProviderErrored>(result).ReasonCode);
        Assert.Equal(2, handler.Requests.Count);
    }

    private static BatchRequest Batch(TimeSpan budget) =>
        new(State: null, [_noul], budget) { Images = [JevImages.Attachment("/ev/home.png", 1512, 982)], ImagesBestEffort = true };

    // Answers each request with the next status after its delay, and keeps what it was sent.
    private sealed class Scripted(IReadOnlyList<(HttpStatusCode Status, TimeSpan Delay)> script) : HttpMessageHandler
    {
        public List<RecordedJevRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await RecordedJevRequest.Capture(request, cancellationToken));
            var (status, delay) = script[Math.Min(Requests.Count, script.Count) - 1];
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            return new HttpResponseMessage(status) { Content = new StringContent(Answer, Encoding.UTF8, "application/json") };
        }
    }
}
