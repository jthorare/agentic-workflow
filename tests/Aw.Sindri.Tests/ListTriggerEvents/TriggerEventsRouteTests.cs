using System.Globalization;
using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Features.PollConnections;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ListTriggerEvents;

/// <summary>
/// GET /sindri/workflows/trigger-events?kind=&amp;connection=&amp;filter=&amp;limit= over the served pipeline (#3201, spec §4.3 and §9 E6): the events of a kind
/// seen on this box, newest first, each with a one-line summary, the trigger payload a run would get, and whether a trigger with the given filter takes it.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TriggerEventsRouteTests : IAsyncLifetime
{
    private const string Route = "/sindri/workflows/trigger-events";

    private static readonly ConnectionName _work = ConnectionSamples.Name("github", "work");
    private static readonly ConnectionName _home = ConnectionSamples.Name("github", "home");
    private static readonly DateTimeOffset _t0 = DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture);

    private GoldenHost? _host;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GoldenHost Host => _host ?? throw new InvalidOperationException("not started");

    public async ValueTask InitializeAsync() => _host = await GoldenHost.Start(temporary: true, Token);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    private void Seen(ConnectionName connection, string key, int minutes, string repo = "acme/app", int number = 42, string title = "Fix login") =>
        Host.App.Services.GetRequiredService<RecentEvents>().Record(connection, new PolledEvent(key, EventKind.GitHubPrMerged, _t0.AddMinutes(minutes), new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["repo"] = repo,
            ["number"] = number.ToString(CultureInfo.InvariantCulture),
            ["title"] = title,
            ["author"] = "ada",
            ["url"] = $"https://github.com/{repo}/pull/{number.ToString(CultureInfo.InvariantCulture)}",
        }));

    private async Task<(HttpStatusCode Status, JsonElement Body)> Read(string query)
    {
        var answer = await Host.Http.Ask(Get(Route + query, Human(Host.Token.Value)));
        return (answer.Status, JsonDocument.Parse(answer.Body).RootElement.Clone());
    }

    [Fact]
    public async Task The_seen_events_of_a_kind_come_newest_first_with_a_summary_and_the_runs_trigger_payload()
    {
        Seen(_work, "github:1", minutes: 1, number: 41, title: "Older");
        Seen(_work, "github:2", minutes: 2, number: 42, title: "Fix login");

        var (status, body) = await Read("?kind=github.pr.merged");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("github.pr.merged", body.GetProperty("kind").GetString());
        var events = body.GetProperty("events").EnumerateArray().ToList();
        Assert.Equal(["github:2", "github:1"], events.Select(static e => e.GetProperty("deliveryId").GetString()), StringComparer.Ordinal);
        var newest = events[0];
        Assert.Equal("github:work", newest.GetProperty("connection").GetString());
        Assert.Equal("acme/app#42 Fix login (ada)", newest.GetProperty("summary").GetString());
        var payload = newest.GetProperty("payload");
        Assert.Equal("github.pr.merged", payload.GetProperty("event").GetString());
        Assert.Equal("github:2", payload.GetProperty("deliveryId").GetString());
        Assert.Equal("Fix login", payload.GetProperty("title").GetString());
        Assert.True(newest.GetProperty("wouldMatch").GetBoolean());   // no filter takes every event
    }

    [Fact]
    public async Task A_connection_narrows_the_list_to_its_events()
    {
        Seen(_work, "github:1", minutes: 1);
        Seen(_home, "github:2", minutes: 2);

        var (_, body) = await Read("?kind=github.pr.merged&connection=github:home");

        Assert.Equal(["github:2"], body.GetProperty("events").EnumerateArray().Select(static e => e.GetProperty("deliveryId").GetString()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_filter_says_for_each_event_whether_the_trigger_would_take_it()
    {
        Seen(_work, "github:1", minutes: 1, repo: "acme/api");
        Seen(_work, "github:2", minutes: 2, repo: "acme/app");

        var (_, body) = await Read("?kind=github.pr.merged&filter=" + Uri.EscapeDataString("trigger.repo == \"acme/api\""));

        var match = body.GetProperty("events").EnumerateArray().ToDictionary(static e => e.GetProperty("deliveryId").GetString()!, static e => e.GetProperty("wouldMatch").GetBoolean(), StringComparer.Ordinal);
        Assert.True(match["github:1"]);
        Assert.False(match["github:2"]);
    }

    [Fact]
    public async Task The_list_holds_ten_events_unless_a_limit_names_another_number()
    {
        for (var i = 1; i <= 15; i++)
        {
            Seen(_work, "github:" + i.ToString(CultureInfo.InvariantCulture), minutes: i);
        }

        var (_, plain) = await Read("?kind=github.pr.merged");
        var (_, five) = await Read("?kind=github.pr.merged&limit=5");

        Assert.Equal(10, plain.GetProperty("events").GetArrayLength());
        Assert.Equal(5, five.GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task A_kind_nothing_was_seen_for_lists_no_events()
    {
        var (status, body) = await Read("?kind=github.push");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, body.GetProperty("events").GetArrayLength());
    }

    [Theory]
    [InlineData("?kind=github.pr.nope")]
    [InlineData("")]
    [InlineData("?kind=github.pr.merged&connection=not-a-name")]
    public async Task An_unknown_or_missing_kind_or_a_bad_connection_is_invalid_input(string query)
    {
        var (status, body) = await Read(query);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("SND-CLI-003", body.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("0")]
    public async Task A_limit_that_is_not_a_whole_number_of_1_or_more_is_invalid_input_not_the_default(string limit)
    {
        var (status, body) = await Read("?kind=github.pr.merged&limit=" + Uri.EscapeDataString(limit));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("SND-CLI-003", body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_workflow_may_not_take_the_slug_trigger_events_because_the_route_would_hide_it()
    {
        var answer = await Host.Http.Ask(Post("/sindri/workflows", "{\"slug\":\"trigger-events\",\"template\":\"manual-notify\"}", Human(Host.Token.Value)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Contains("reserved", answer.Body, StringComparison.Ordinal);
    }
}
