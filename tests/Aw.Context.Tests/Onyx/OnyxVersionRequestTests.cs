using System.Net;
using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Onyx;

namespace Aw.Context.Tests.Onyx;

/// <summary>What the version probe sends and how it reads a reply it can't use, over an in-memory handler.</summary>
public sealed class OnyxVersionRequestTests
{
    private static readonly OnyxSettings _pinned = OnyxSettings.Create("http://127.0.0.1:3189", "env:ONYX_FIXTURE_KEY", "v4.9.0").Value;

    [Fact, Trait("Lane", "unit")]
    public async Task It_gets_api_version_under_the_base_url_with_no_authorization_header()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Reply(HttpStatusCode.OK, "{\"backend_version\":\"v4.9.0\"}")));
        using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Authorization = null;

        var health = await new OnyxVersionProbe(http).Check(_pinned, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(SourceState.Ok, health.State);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("http://127.0.0.1:3189/api/version"), request.Uri);
        Assert.False(request.HadAuthorization);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_answer_within_the_deadline_is_unavailable_with_010()
    {
        var handler = new ScriptedHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Reply(HttpStatusCode.OK, "{}");
        });
        using var http = new HttpClient(handler);

        var health = await new OnyxVersionProbe(http).Check(_pinned, TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        Assert.Equal(SourceState.Unavailable, health.State);
        Assert.Equal(ContextErrors.SourceUnavailable.Code, health.Code);
        Assert.Contains("127.0.0.1:3189", health.Detail, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HttpStatusCode.NotFound, "{\"error_code\":\"NOT_FOUND\",\"detail\":\"Not Found\"}", "404")]
    [InlineData(HttpStatusCode.OK, "<html>not onyx</html>", "backend_version")]
    [InlineData(HttpStatusCode.OK, "{\"version\":\"v4.9.0\"}", "backend_version")]
    [InlineData(HttpStatusCode.OK, "{\"backend_version\":null}", "backend_version")]
    public async Task A_reply_that_is_not_onyxs_version_is_unavailable_with_010(HttpStatusCode status, string body, string says)
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Reply(status, body)));
        using var http = new HttpClient(handler);

        var health = await new OnyxVersionProbe(http).Check(_pinned, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(SourceState.Unavailable, health.State);
        Assert.Equal(ContextErrors.SourceUnavailable.Code, health.Code);
        Assert.Contains(says, health.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_callers_cancellation_is_not_turned_into_a_health_answer()
    {
        var handler = new ScriptedHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Reply(HttpStatusCode.OK, "{}");
        });
        using var http = new HttpClient(handler);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await new OnyxVersionProbe(http).Check(_pinned, TimeSpan.FromSeconds(30), cancel.Token));
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
