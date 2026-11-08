using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class EventPollerTests
{
    [Theory]
    [InlineData("github", FakeProvider.GitHubHost)]
    [InlineData("linear", FakeProvider.LinearHost)]
    [InlineData("slack", FakeProvider.SlackHost)]
    [InlineData("google", FakeProvider.GoogleHost)]
    public async Task The_token_is_sent_only_to_the_preset_host(string preset, string host)
    {
        var provider = new FakeProvider();
        provider.RedirectTo(new Uri("https://evil.example/collect"));
        using var h = PollerHarness.For(preset, provider);

        var polled = await h.Poller.Poll(h.Connection, h.Kind, cursor: null, TestContext.Current.CancellationToken);

        Assert.True(polled.IsError);
        Assert.Equal("SND-CONN-012", polled.Error.Code);
        var request = Assert.Single(provider.Requests);
        Assert.Equal(host, request.Uri.Host);
        Assert.DoesNotContain(provider.Requests, static r => string.Equals(r.Uri.Host, "evil.example", StringComparison.Ordinal));
        Assert.DoesNotContain(PollerHarness.Token, polled.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("github")]
    [InlineData("linear")]
    [InlineData("slack")]
    [InlineData("google")]
    public async Task A_429_with_Retry_After_is_a_rate_limited_result_that_keeps_the_cursor(string preset)
    {
        var provider = new FakeProvider();
        provider.RateLimit(times: 1, retryAfter: TimeSpan.FromSeconds(45));
        using var h = PollerHarness.For(preset, provider);

        var polled = await h.Poller.Poll(h.Connection, h.Kind, cursor: "500", TestContext.Current.CancellationToken);

        Assert.True(polled.IsSuccess);
        Assert.True(polled.Value.RateLimited);
        Assert.Equal(TimeSpan.FromSeconds(45), polled.Value.RetryAfter);
        Assert.Empty(polled.Value.Events);
        Assert.Equal("500", polled.Value.NextCursor);
    }

    [Theory]
    [InlineData("github")]
    [InlineData("linear")]
    [InlineData("slack")]
    [InlineData("google")]
    public async Task A_503_without_Retry_After_is_rate_limited_with_no_wait_named(string preset)
    {
        var provider = new FakeProvider();
        provider.RateLimit(times: 1, retryAfter: null, status: HttpStatusCode.ServiceUnavailable);
        using var h = PollerHarness.For(preset, provider);

        var polled = await h.Poller.Poll(h.Connection, h.Kind, cursor: null, TestContext.Current.CancellationToken);

        Assert.True(polled.IsSuccess);
        Assert.True(polled.Value.RateLimited);
        Assert.Null(polled.Value.RetryAfter);
    }
}
