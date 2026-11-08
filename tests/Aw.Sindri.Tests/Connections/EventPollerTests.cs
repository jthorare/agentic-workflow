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

    [Theory]
    [InlineData("github")]
    [InlineData("linear")]
    [InlineData("slack")]
    [InlineData("google")]
    public async Task A_provider_that_cannot_be_reached_says_so_and_a_429_says_rate_limiting(string preset)
    {
        var down = new FakeProvider();
        down.Unreachable();
        var limited = new FakeProvider();
        limited.RateLimit(times: 1, retryAfter: null);
        using var offline = PollerHarness.For(preset, down);
        using var busy = PollerHarness.For(preset, limited);
        var ct = TestContext.Current.CancellationToken;

        var unreachable = await offline.Poller.Poll(offline.Connection, offline.Kind, cursor: "500", ct);
        var throttled = await busy.Poller.Poll(busy.Connection, busy.Kind, cursor: "500", ct);

        Assert.True(unreachable.Value.RateLimited);
        Assert.Contains("could not be reached", unreachable.Value.Reason, StringComparison.Ordinal);
        Assert.True(throttled.Value.RateLimited);
        Assert.Contains("rate limiting", throttled.Value.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Retry_After_given_as_an_HTTP_date_is_read_against_the_clock()
    {
        var provider = new FakeProvider();
        provider.RateLimit(times: 1, retryAfter: null, headers: new Dictionary<string, string>(StringComparer.Ordinal) { ["Retry-After"] = "Thu, 01 Jan 2026 00:01:00 GMT" });
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, h.Kind, cursor: "2026-11-08T09:00:00Z", TestContext.Current.CancellationToken);

        Assert.True(polled.Value.RateLimited);
        Assert.Equal(TimeSpan.FromMinutes(1), polled.Value.RetryAfter);   // the test clock stands at 2026-01-01T00:00:00Z
    }

    [Theory]
    [InlineData("github")]
    [InlineData("linear")]
    [InlineData("slack")]
    [InlineData("google")]
    public async Task A_503_names_its_status_and_a_429_says_rate_limiting_and_both_keep_the_backoff(string preset)
    {
        var down = new FakeProvider();
        down.RateLimit(times: 1, retryAfter: null, HttpStatusCode.ServiceUnavailable);
        var busy = new FakeProvider();
        busy.RateLimit(times: 1, retryAfter: null);
        using var failing = PollerHarness.For(preset, down);
        using var throttled = PollerHarness.For(preset, busy);
        var ct = TestContext.Current.CancellationToken;

        var five = await failing.Poller.Poll(failing.Connection, failing.Kind, cursor: "500", ct);
        var limited = await throttled.Poller.Poll(throttled.Connection, throttled.Kind, cursor: "500", ct);

        Assert.True(five.Value.RateLimited);   // the backoff still applies
        Assert.Contains("503", five.Value.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("rate limiting", five.Value.Reason, StringComparison.Ordinal);
        Assert.True(limited.Value.RateLimited);
        Assert.Contains("rate limiting", limited.Value.Reason, StringComparison.Ordinal);
    }
}
