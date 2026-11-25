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

    // Answers every request with one status and body, and records the last request.
    private sealed class Scripted(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public bool Throws { get; init; }

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

            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
