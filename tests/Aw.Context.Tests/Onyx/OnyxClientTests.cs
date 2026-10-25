using System.Net;
using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Context.Infrastructure.Onyx;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Onyx;

/// <summary>How the onyx client sends a request and reads replies that aren't what v4.9.0 returns, over an in-memory handler.</summary>
public sealed class OnyxClientTests
{
    private static readonly OnyxSettings _settings = OnyxSettings.Create("http://127.0.0.1:3189", OnyxSourceHarness.Pointer, "v4.9.0").Value;

    [Fact, Trait("Lane", "unit")]
    public async Task It_sends_the_resolved_key_as_bearer_to_the_route_under_the_base_url()
    {
        string? authorization = null;
        var handler = new ScriptedHandler((request, _) =>
        {
            authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(Reply(HttpStatusCode.OK, "{\"documents\":[]}"));
        });

        var hits = await Search(handler);

        Assert.Empty(hits.Value);
        Assert.Equal("Bearer on_fixture", authorization);
        var seen = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Post, new Uri("http://127.0.0.1:3189/api/admin/search")), (seen.Method, seen.Uri));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HttpStatusCode.OK, "{\"results\":[]}")]
    [InlineData(HttpStatusCode.OK, "{\"documents\":[{\"document_id\":\"x\"}]}")]
    [InlineData(HttpStatusCode.OK, "<html>not onyx</html>")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "{\"error_code\":\"VALIDATION_ERROR\",\"detail\":\"Request validation failed.\"}")]
    [InlineData(HttpStatusCode.NotFound, "{\"error_code\":\"NOT_FOUND\",\"detail\":\"Not Found\"}")]
    public async Task A_reply_that_is_not_v4_9_0s_is_051_naming_the_pinned_version(HttpStatusCode status, string body)
    {
        var result = await Search(new ScriptedHandler((_, _) => Task.FromResult(Reply(status, body))));

        Assert.Equal(ContextErrors.OnyxUnexpectedReply.Code, result.Error.Code);
        Assert.Equal(Kernel.ErrorKind.Unavailable, result.Error.Kind);
        Assert.Contains("v4.9.0", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_401_or_403_is_a_refused_key_with_the_fix_not_a_version_mismatch(HttpStatusCode status)
    {
        var result = await Search(new ScriptedHandler((_, _) => Task.FromResult(Reply(status, "{}"))));

        Assert.Equal(ContextErrors.OnyxKeyRefused.Code, result.Error.Code);
        Assert.Contains("Fix:", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains($"HTTP {((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture)}", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("onyx.version", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_server_error_is_unavailable_with_010(HttpStatusCode status)
    {
        var result = await Search(new ScriptedHandler((_, _) => Task.FromResult(Reply(status, "{}"))));

        Assert.Equal(ContextErrors.SourceUnavailable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_key_with_a_line_break_is_002_and_never_sent()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Reply(HttpStatusCode.OK, "{\"documents\":[]}")));

        var result = await Search(handler, token: "on_fixture\r\nX-Evil: 1");

        Assert.Equal(ContextErrors.SourceNotConfigured.Code, result.Error.Code);
        Assert.DoesNotContain("X-Evil", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("the env: secret pointer", result.Error.Message, StringComparison.Ordinal);   // the scheme, never the target or a value
        Assert.Contains("'sindri doctor'", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ONYX_FIXTURE_KEY", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_callers_cancellation_is_thrown_not_turned_into_an_error()
    {
        var handler = new ScriptedHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Reply(HttpStatusCode.OK, "{}");
        });
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Search(handler, cancellation: cancel.Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_already_cancelled_caller_gets_the_cancellation_even_when_the_deadline_is_spent()
    {
        using var http = new HttpClient(new ScriptedHandler((_, _) => Task.FromResult(Reply(HttpStatusCode.OK, "{}"))));
        var parts = new OnyxSourceParts(http, _ => "on_fixture", new FakeTimeProvider(OnyxSourceHarness.Now), new CapturingLogger<OnyxClient>());
        var client = new OnyxClient(_settings, parts);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await client.Call<object>(OnyxCall.Get("/api/x"), TimeSpan.Zero, cancel.Token));
    }

    private static async Task<Kernel.Result<IReadOnlyList<Context.Core.Sources.SourceHit>>> Search(
        ScriptedHandler handler, string? token = "on_fixture", CancellationToken? cancellation = null)
    {
        using var http = new HttpClient(handler);
        var parts = new OnyxSourceParts(http, name => name == OnyxSourceHarness.Pointer ? token : null, new FakeTimeProvider(OnyxSourceHarness.Now), new CapturingLogger<OnyxClient>());
        var source = new OnyxSources(parts).Create(_settings, new HashSet<string>(StringComparer.Ordinal) { "SPL" });
        return await source.Search(OnyxSourceHarness.Query("pane"), cancellation ?? TestContext.Current.CancellationToken);
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
