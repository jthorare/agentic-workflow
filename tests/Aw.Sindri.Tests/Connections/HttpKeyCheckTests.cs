using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>#4357: the one call per service that checks a pasted key, against a scripted handler (no network). The key goes only into the Authorization header.</summary>
[Lane(TestLane.Unit)]
public sealed class HttpKeyCheckTests
{
    private const string Key = "pasted-key-0001";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ConnectionPreset Preset(string id) => new EmbeddedConnectionPresets().Find(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public async Task GitHub_is_a_GET_of_the_user_with_a_Bearer_key_and_a_user_agent()
    {
        using var handler = new Scripted(HttpStatusCode.OK, """{"login":"ada"}""");
        using var http = new HttpClient(handler);

        var checkedKey = await new HttpKeyCheck(http).Check(Preset("github"), Key, Token);

        Assert.True(checkedKey.IsSuccess);
        Assert.Equal(("GET", "https://api.github.com/user", "Bearer " + Key), (handler.Method, handler.Url, handler.Authorization));
        Assert.True(handler.HasUserAgent);
    }

    [Fact]
    public async Task Linear_is_a_POST_of_the_viewer_with_the_key_as_it_is()
    {
        using var handler = new Scripted(HttpStatusCode.OK, """{"data":{"viewer":{"id":"u1"}}}""");
        using var http = new HttpClient(handler);

        var checkedKey = await new HttpKeyCheck(http).Check(Preset("linear"), Key, Token);

        Assert.True(checkedKey.IsSuccess);
        Assert.Equal(("POST", HttpKeyCheck.LinearGraphQl, Key), (handler.Method, handler.Url, handler.Authorization));
        Assert.Contains("viewer", handler.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("slack", HttpStatusCode.OK, """{"ok":false,"error":"invalid_auth"}""")]
    [InlineData("linear", HttpStatusCode.OK, """{"errors":[{"message":"Authentication required"}]}""")]
    [InlineData("linear", HttpStatusCode.BadRequest, """{"errors":[]}""")]
    [InlineData("github", HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}""")]
    [InlineData("github", HttpStatusCode.Forbidden, "{}")]
    public async Task A_service_that_says_no_is_SND_CONN_018_and_the_error_holds_neither_the_key_nor_the_body(string preset, HttpStatusCode status, string body)
    {
        using var handler = new Scripted(status, body);
        using var http = new HttpClient(handler);

        var checkedKey = await new HttpKeyCheck(http).Check(Preset(preset), Key, Token);

        Assert.Equal("SND-CONN-018", checkedKey.Error.Code);
        Assert.DoesNotContain(Key, checkedKey.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid_auth", checkedKey.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Slack_with_ok_true_takes_the_key()
    {
        using var handler = new Scripted(HttpStatusCode.OK, """{"ok":true,"user":"ada"}""");
        using var http = new HttpClient(handler);

        var checkedKey = await new HttpKeyCheck(http).Check(Preset("slack"), Key, Token);

        Assert.True(checkedKey.IsSuccess);
        Assert.Equal(("GET", "https://slack.com/api/auth.test", "Bearer " + Key), (handler.Method, handler.Url, handler.Authorization));
    }

    [Fact]
    public async Task A_5xx_or_no_network_is_SND_CONN_019()
    {
        using var failing = new Scripted(HttpStatusCode.ServiceUnavailable, string.Empty);
        using var down = new Scripted(HttpStatusCode.OK, string.Empty) { Throws = true };
        using var http5xx = new HttpClient(failing);
        using var httpDown = new HttpClient(down);

        Assert.Equal("SND-CONN-019", (await new HttpKeyCheck(http5xx).Check(Preset("github"), Key, Token)).Error.Code);
        Assert.Equal("SND-CONN-019", (await new HttpKeyCheck(httpDown).Check(Preset("github"), Key, Token)).Error.Code);
    }

    [Fact]
    public async Task A_preset_with_no_check_is_taken_without_a_call()
    {
        using var handler = new Scripted(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(handler);
        var bare = Preset("spotify") with { ProbeUrl = null };

        var checkedKey = await new HttpKeyCheck(http).Check(bare, Key, Token);

        Assert.True(checkedKey.IsSuccess);
        Assert.Null(handler.Method);
    }

    [Fact]
    public async Task A_service_that_never_answers_is_SND_CONN_019_once_the_check_times_out()
    {
        using var handler = new Scripted(HttpStatusCode.OK, """{"login":"ada"}""") { Delay = TimeSpan.FromSeconds(5) };
        using var http = new HttpClient(handler);

        var checkedKey = await new HttpKeyCheck(http, TimeSpan.FromMilliseconds(50)).Check(Preset("github"), Key, Token);

        Assert.Equal("SND-CONN-019", checkedKey.Error.Code);
        Assert.Contains("did not answer", checkedKey.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_check_waits_15_seconds_by_default() => Assert.Equal(TimeSpan.FromSeconds(15), new HttpKeyCheck(new HttpClient()).Timeout);

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "retry-after", "60")]
    [InlineData(HttpStatusCode.Forbidden, "x-ratelimit-remaining", "0")]
    [InlineData(HttpStatusCode.TooManyRequests, "retry-after", "60")]
    [InlineData(HttpStatusCode.TooManyRequests, null, null)]
    public async Task A_rate_limit_is_SND_CONN_019_that_says_wait_not_a_refused_key(HttpStatusCode status, string? header, string? value)
    {
        using var handler = new Scripted(status, """{"message":"You have exceeded a secondary rate limit"}""") { Header = header, HeaderValue = value };
        using var http = new HttpClient(handler);

        var checkedKey = await new HttpKeyCheck(http).Check(Preset("github"), Key, Token);

        Assert.Equal("SND-CONN-019", checkedKey.Error.Code);
        Assert.Contains("rate limiting", checkedKey.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secondary", checkedKey.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_403_with_rate_limit_left_is_still_a_refused_key()
    {
        using var handler = new Scripted(HttpStatusCode.Forbidden, "{}") { Header = "x-ratelimit-remaining", HeaderValue = "4999" };
        using var http = new HttpClient(handler);

        Assert.Equal("SND-CONN-018", (await new HttpKeyCheck(http).Check(Preset("github"), Key, Token)).Error.Code);
    }

    // Answers every request with one status and body, and records the last request.
    private sealed class Scripted(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public bool Throws { get; init; }

        public TimeSpan Delay { get; init; }

        public string? Header { get; init; }

        public string? HeaderValue { get; init; }

        public string? Method { get; private set; }

        public string? Url { get; private set; }

        public string? Authorization { get; private set; }

        public string Body { get; private set; } = string.Empty;

        public bool HasUserAgent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            (Method, Url, HasUserAgent) = (request.Method.Method, request.RequestUri?.ToString(), request.Headers.UserAgent.Count > 0);
            Authorization = request.Headers.TryGetValues("Authorization", out var values) ? string.Join(' ', values) : null;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            if (Throws)
            {
                throw new HttpRequestException("no network");
            }

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (Header is not null)
            {
                _ = response.Headers.TryAddWithoutValidation(Header, HeaderValue);
            }

            return response;
        }
    }
}
