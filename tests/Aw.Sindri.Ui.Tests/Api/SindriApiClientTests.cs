using System.Net;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

[Lane(TestLane.Unit)]
public sealed class SindriApiClientTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly InMemoryHumanToken _token = new(A.HumanToken + "\n");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_error_status_is_an_answer_the_app_cant_read()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.InternalServerError, """{"title":"SND-LEDGER-002"}""") };

        var status = await new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System).Status(_api, Token);

        Assert.Equal("SND-UI-003", status.Error.Code);
        Assert.Contains("500", status.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_body_that_isnt_the_record_is_an_answer_the_app_cant_read()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.OK, "<html>not sindri</html>") };

        var ping = await new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System).Ping(_api, Token);

        Assert.Equal("SND-UI-003", ping.Error.Code);
    }

    // Prover run 7 (§4.1 Unreadable, §12.4, as I7 for SND-UI-002): a body this build can't read is a plain sentence naming
    // the route, never the parser's text (a .NET type name, a JSON path, a byte position). That text goes to the app's log.
    [Fact]
    public async Task A_body_this_build_cant_read_is_a_plain_sentence_and_the_parsers_text_goes_to_the_log()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.OK, """{"pid":"not-a-number","version":7}""") };
        var log = new RecordingLogger();

        var status = await new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System, log).Status(_api, Token);

        Assert.Equal("SND-UI-003", status.Error.Code);
        Assert.Equal("/sindri/status answered with something this build can't read", status.Error.Message);
        Assert.All((string[])["System.", "Path:", "BytePosition", "$."], raw => Assert.DoesNotContain(raw, status.Error.Message, StringComparison.Ordinal));
        var entry = Assert.Single(log.Entries);
        Assert.Equal(("SND-UI-003", LogLevel.Warning), (entry.EventName, entry.Level));
        Assert.Contains("/sindri/status", entry.Message, StringComparison.Ordinal);
        Assert.Contains("BytePosition", entry.Message, StringComparison.Ordinal);
    }

    // Proof B6: the shell re-reads on every event; one unreadable route is one log line, not one for each event.
    [Fact]
    public async Task The_same_unreadable_answer_is_logged_once_and_a_different_one_again()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.OK, """{"pid":"not-a-number","version":7}""") };
        var log = new RecordingLogger();
        var client = new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System, log);

        _ = await client.Status(_api, Token);
        _ = await client.Status(_api, Token);
        _ = await client.Status(_api, Token);
        _ = Assert.Single(log.Entries);

        handler.Override = (HttpStatusCode.OK, """{"pid":1,"version":{"x":1}}""");
        _ = await client.Status(_api, Token);

        Assert.Equal(2, log.Entries.Count);
    }

    // review run 1, test-9: the key is the route as well as the message; two routes failing in turn log once each.
    [Fact]
    public async Task Two_routes_that_both_fail_are_logged_once_each_not_on_every_turn()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.OK, """{"pid":"not-a-number","version":7}""") };
        var log = new RecordingLogger();
        var client = new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System, log);

        for (var i = 0; i < 3; i++)
        {
            _ = await client.Status(_api, Token);
            _ = await client.Services(_api, Token);
        }

        Assert.Equal(2, log.Entries.Count);
        Assert.Contains(log.Entries, e => e.Message.Contains("/sindri/status", StringComparison.Ordinal));
        Assert.Contains(log.Entries, e => e.Message.Contains("/sindri/services", StringComparison.Ordinal));
    }

    // The line comes back when the route has been readable in between: a recurrence is news.
    [Fact]
    public async Task A_recurrence_after_a_good_answer_is_logged_again()
    {
        var api = new FakeSindriApi();
        var handler = new FakeSindriHandler(api) { Override = (HttpStatusCode.OK, """{"pid":"not-a-number","version":7}""") };
        var log = new RecordingLogger();
        var client = new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System, log);
        _ = await client.Status(_api, Token);

        handler.Override = null;
        Assert.True((await client.Status(_api, Token)).IsSuccess);
        handler.Override = (HttpStatusCode.OK, """{"pid":"not-a-number","version":7}""");
        _ = await client.Status(_api, Token);

        Assert.Equal(2, log.Entries.Count);
    }

    // API §6: a refused call never carries the token into its error or the log (the token goes only into its header).
    [Fact]
    public async Task A_refusal_names_no_token_in_its_error_or_the_log()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi { Refusal = A.StaleTokenRefusal });
        var log = new RecordingLogger();

        var status = await new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System, log).Status(_api, Token);

        Assert.Equal(("SND-AUTH-001", ErrorKind.Forbidden), (status.Error.Code, status.Error.Kind));
        Assert.DoesNotContain(A.HumanToken, status.Error.Message, StringComparison.Ordinal);
        Assert.All(log.Entries, e => Assert.DoesNotContain(A.HumanToken, e.Message, StringComparison.Ordinal));
    }

    // Orchestrator decision (PR #184): a 403 SND-AUTH-001 is retried exactly once with the token read again from
    // auth/human.token, since a restarted host has minted a new one: a stale token, then the fresh one, is one retry and
    // the answer.
    [Fact]
    public async Task A_stale_token_is_read_again_and_the_call_retried_once()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { RequiredToken = "fresh-start-token" };
        _token.Then("stale-start-token", "fresh-start-token");
        var client = new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System);

        var status = await client.Status(_api, Token);

        Assert.True(status.IsSuccess);
        Assert.Equal(["stale-start-token", "fresh-start-token"], handler.Requests.Select(r => r.Token), StringComparer.Ordinal);
    }

    // Refused twice: Sindri's code and detail as they are (the window's Unreadable banner), with no third call; the
    // event stream does the same and ends.
    [Fact]
    public async Task Refused_twice_is_sindris_code_and_detail_with_no_third_call()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { RequiredToken = "fresh-start-token" };
        var client = new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System);

        var ping = await client.Ping(_api, Token);
        var events = await client.Events(_api, 0, Token).ToListAsync(Token);

        Assert.Equal(new Error("SND-AUTH-001", ErrorKind.Forbidden, A.StaleTokenRefusal.Detail), ping.Error);
        Assert.Empty(events);
        Assert.Equal(["/sindri/ping", "/sindri/ping", "/sindri/events", "/sindri/events"], handler.Requests.Select(r => r.Path), StringComparer.Ordinal);
        Assert.Equal(4, _token.Reads);
    }

    // Only a 403 with Sindri's ProblemDetails (a title) is a refusal; another 403 body is still an answer this build can't read.
    [Fact]
    public async Task A_403_without_a_problem_is_still_an_answer_the_app_cant_read()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.Forbidden, "<html>forbidden</html>") };

        var ping = await new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System).Ping(_api, Token);

        Assert.Equal(("SND-UI-003", "/sindri/ping answered 403"), (ping.Error.Code, ping.Error.Message));
    }

    // I7: nothing listening is SND-UI-002 without the platform's exception text (it repeated the address in .NET's words).
    [Fact]
    public async Task Nothing_listening_is_unreachable_without_the_exception_text()
    {
        var server = new FakeSindriApi();
        server.GoDown();

        var ping = await new SindriApiClient(new HttpClient(new FakeSindriHandler(server)), _token, TimeProvider.System).Ping(_api, Token);

        Assert.Equal("SND-UI-002", ping.Error.Code);
        Assert.DoesNotContain("Connection refused", ping.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_event_stream_that_isnt_ok_ends_at_once()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.NotFound, "{}") };

        var events = await new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System).Events(_api, 0, Token).ToListAsync(Token);

        Assert.Empty(events);
    }

    // UI spec §9.1, U2, API spec §6: the app is a human with no terminal: X-Sindri-Caller human, X-Sindri-Client ui and
    // this start's X-Sindri-Token on every request, never X-Sindri-Terminal and never Origin.
    [Fact]
    public async Task Every_request_says_human_from_the_ui_with_this_starts_token_and_never_terminal_or_origin()
    {
        var server = new FakeSindriApi();
        server.Push(A.Event(1));
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System);

        _ = await client.Ping(_api, Token);
        _ = await client.Status(_api, Token);
        await foreach (var _ in client.Events(_api, 0, Token))
        {
            server.Drop();
        }

        Assert.Equal(["/sindri/ping", "/sindri/status", "/sindri/events"], handler.Requests.Select(r => r.Path), StringComparer.Ordinal);
        Assert.All(handler.Requests, r => Assert.Equal(new SeenRequest(r.Path, "human", "ui", Terminal: null, Origin: null, A.HumanToken), r with { Query = "" }));
    }

    // API spec §6, docs/csharp/sindri.md: the token is read at each call, like the CLI's, so a restarted control plane's
    // new token is sent with no app restart.
    [Fact]
    public async Task The_token_is_read_at_each_call_so_a_restarted_control_planes_token_is_sent()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System);

        _ = await client.Ping(_api, Token);
        _token.Write("next-start-human-token\n");
        _ = await client.Ping(_api, Token);

        Assert.Equal([A.HumanToken, "next-start-human-token"], handler.Requests.Select(r => r.Token), StringComparer.Ordinal);
    }

    // Like the CLI (docs/csharp/sindri.md): with no token file, or one the platform can't read, the call goes without the
    // token and Sindri's refusal says why. An unreadable file is logged with its path and the exception's type only.
    [Fact]
    public async Task No_token_file_or_an_unreadable_one_sends_none_and_the_unreadable_one_is_logged()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var log = new RecordingLogger();
        var client = new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System, log);

        _token.Write(contents: null);
        _ = await client.Ping(_api, Token);
        _token.Write(A.HumanToken);
        _token.FailReads(new UnauthorizedAccessException("Access to the path '/h/auth/human.token' is denied."));
        _ = await client.Status(_api, Token);

        Assert.Equal([null, null], handler.Requests.Select(r => r.Token), StringComparer.Ordinal);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(_token.Location, entry.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(UnauthorizedAccessException), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(A.HumanToken, entry.Message, StringComparison.Ordinal);
    }

    // SND-UI-002's 120 s rule runs on the app's clock, like every other timing rule: a call that hangs is unreachable
    // once the clock passes 120 s, with no real wait; a slow answer (a context read of a large map) is not.
    [Fact]
    public async Task A_call_that_doesnt_answer_within_120_s_on_the_apps_clock_is_unreachable()
    {
        var clock = new FakeTimeProvider(A.Noon);
        using var http = new HttpClient(new SilentHandler());
        var ping = new SindriApiClient(http, _token, clock).Ping(_api, Token);

        clock.Advance(TimeSpan.FromSeconds(120) - TimeSpan.FromTicks(1));
        Assert.False(ping.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        var result = await ping.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, Token);

        Assert.Equal("SND-UI-002", result.Error.Code);
        Assert.Contains("didn't answer within 120 s", result.Error.Message, StringComparison.Ordinal);
    }

    // A server that accepts the request and never answers.
    private sealed class SilentHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }
}
