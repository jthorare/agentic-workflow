using System.Net;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Server;

/// <summary>GET /sindri/events on a real loopback host with a frozen clock: the cursor rules, the refusals before the first frame, the wake on an append, the stream limits and the stop.</summary>
public sealed class EventStreamEndpointTests
{
    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Last_event_id_wins_over_after()
    {
        await using var host = await HostWithHeartbeats(3, TestContext.Current.CancellationToken);   // sequences 1 (process.started) and 2-4

        await using var stream = await EventStreamClient.Open(host, "/sindri/events?after=0", ("Last-Event-ID", "3"));

        Assert.Equal(["event: service.heartbeat", "id: 4"], [await stream.Line(), await stream.Line()]);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Resume_at_n_starts_at_n_plus_1()
    {
        await using var host = await HostWithHeartbeats(3, TestContext.Current.CancellationToken);

        await using var stream = await EventStreamClient.Open(host, "/sindri/events", ("Last-Event-ID", "1"));

        Assert.Equal(["event: service.heartbeat", "id: 2"], [await stream.Line(), await stream.Line()]);
    }

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [InlineData("/sindri/events?after=99", null)]
    [InlineData("/sindri/events", "99")]
    [InlineData("/sindri/events?after=-1", null)]
    [InlineData("/sindri/events?types=Service.*", null)]
    [InlineData("/sindri/events?item=nope", null)]
    [InlineData("/sindri/events?instance=nope", null)]
    public async Task A_bad_cursor_or_filter_is_a_400_before_any_frame(string route, string? lastEventId)
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        using var request = Get(route, Human(host.Token.Value));
        if (lastEventId is not null)
        {
            _ = request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
        }

        var answer = await host.Http.Ask(request);

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003"), (answer.Status, answer.Code));
    }

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [InlineData("/sindri/events?after=-1", null)]
    [InlineData("/sindri/ledger?after=-1", null)]
    public async Task A_negative_after_is_named_as_negative(string route, string? lastEventId)
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        using var request = Get(route, Human(host.Token.Value));
        if (lastEventId is not null)
        {
            _ = request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
        }

        var answer = await host.Http.Ask(request);

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003", true), (answer.Status, answer.Code, answer.Body.Contains("negative; a cursor is 0 or more", StringComparison.Ordinal)));
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task An_append_reaches_the_stream_without_waiting_for_the_poll()
    {
        await using var host = await HostWithHeartbeats(0, TestContext.Current.CancellationToken);   // the clock never moves in this test
        await using var stream = await EventStreamClient.Open(host, "/sindri/events?after=1");

        await Heartbeat(host, 1, TestContext.Current.CancellationToken);

        Assert.Equal(["event: service.heartbeat", "id: 2"], [await stream.Line(), await stream.Line()]);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_65th_stream_is_429_SND_API_012()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        var open = await OpenMany(host, StreamLimits.Total, Human(host.Token.Value));

        var overTotal = await host.Http.Ask(Get("/sindri/events", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.TooManyRequests, "SND-API-012"), (overTotal.Status, overTotal.Code));
        await DisposeAll(open);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_session_gets_the_same_streams_as_a_human()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        var session = new[] { (SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli") };
        var open = await OpenMany(host, StreamLimits.Total - 1, session);

        await using var last = await EventStreamClient.Open(host, "/sindri/events?after=0", session);
        var over = await host.Http.Ask(Get("/sindri/events", session));

        Assert.StartsWith("event: ", await last.Line(), StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.TooManyRequests, "SND-API-012"), (over.Status, over.Code));
        await DisposeAll(open);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_closed_stream_frees_its_slot()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        var open = await OpenMany(host, StreamLimits.Total, Human(host.Token.Value));
        await open[0].DisposeAsync();

        var admitted = HttpStatusCode.TooManyRequests;
        for (var attempt = 0; attempt < 100 && admitted == HttpStatusCode.TooManyRequests; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, TestContext.Current.CancellationToken);   // the server notices the closed socket a moment later
            using var request = Get("/sindri/events", Human(host.Token.Value));
            using var response = await host.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
            admitted = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, admitted);
        await DisposeAll(open.Skip(1));
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_stop_ends_open_streams()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        await using var stream = await EventStreamClient.Open(host, "/sindri/events?after=1");

        var stop = await host.Http.Ask(Post("/sindri/stop", json: null, Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, stop.Status);
        // The stop appends process.stopped before it asks for the shutdown, so a stream that wakes on that append may write its frame
        // first, depending on who runs first (the stream reads it only if the shutdown hasn't cancelled it yet). Either way the body ends:
        // read to the end, and the only event a stream may have sent is the stop itself.
        var lines = new List<string>();
        while (await stream.Line() is { } line)
        {
            lines.Add(line);
            Assert.True(lines.Count < 20, "the stream kept writing after a stop: " + string.Join(" | ", lines));
        }

        Assert.All(lines.Where(static l => l.StartsWith("event:", StringComparison.Ordinal)), static l => Assert.Equal("event: process.stopped", l));
    }

    private static async Task<GoldenHost> HostWithHeartbeats(int count, CancellationToken ct)
    {
        var host = await GoldenHost.Start(temporary: true, ct);
        for (var i = 0; i < count; i++)
        {
            await Heartbeat(host, i, ct);
        }

        return host;
    }

    private static Task Heartbeat(GoldenHost host, int slot, CancellationToken ct)
    {
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, slot, TimeSpan.Zero);
        return ViewScenarios.Append(host, "service.heartbeat", new Heartbeat(at, at), "service.heartbeat:1:" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
    }

    private static async Task<List<EventStreamClient>> OpenMany(GoldenHost host, int count, params (string Name, string Value)[] headers)
    {
        var open = new List<EventStreamClient>();
        for (var i = 0; i < count; i++)
        {
            open.Add(await EventStreamClient.Open(host, "/sindri/events?after=1", headers));
        }

        return open;
    }

    private static async Task DisposeAll(IEnumerable<EventStreamClient> open)
    {
        foreach (var stream in open)
        {
            await stream.DisposeAsync();
        }
    }
}
