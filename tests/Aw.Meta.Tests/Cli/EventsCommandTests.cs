using System.Text.Json;
using Aw.Meta.Core.Events;
using Aw.Meta.Infrastructure.Sqlite;

namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class EventsCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private async Task Seed()
    {
        var events = new SqliteMetaEventStore(new MetaDatabase(MetaDatabase.OptionsFor(_cli.File("meta")), _cli.Clock));
        _ = await events.Append(new MetaEvent(0, new DateTimeOffset(2026, 10, 15, 5, 0, 0, TimeSpan.Zero), "obligation.opened", "api", "{\"id\":\"m1\"}"), TestContext.Current.CancellationToken);
        _ = await events.Append(new MetaEvent(0, new DateTimeOffset(2026, 10, 15, 5, 1, 0, TimeSpan.Zero), "box.health", Lane: null, "{\"mem_available_mb\":9000}"), TestContext.Current.CancellationToken);
        _ = await events.Append(new MetaEvent(0, new DateTimeOffset(2026, 10, 15, 5, 2, 0, TimeSpan.Zero), "lane.down", "ui", "{\"lane\":\"ui\",\"reason\":\"idle\"}"), TestContext.Current.CancellationToken);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Events_prints_one_line_per_event_with_a_dash_for_no_lane_and_needs_no_team()
    {
        await Seed();

        var shown = await _cli.Run("events");

        Assert.Equal(0, shown.Exit);
        Assert.Equal(
            [
                "1 2026-10-15T05:00:00Z obligation.opened api {\"id\":\"m1\"}",
                "2 2026-10-15T05:01:00Z box.health - {\"mem_available_mb\":9000}",
                "3 2026-10-15T05:02:00Z lane.down ui {\"lane\":\"ui\",\"reason\":\"idle\"}",
            ],
            shown.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Events_json_carries_the_events_and_a_next_cursor_to_pass_back_as_after()
    {
        await Seed();

        var first = await _cli.Run("events", "--max", "2", "--json");
        using var firstDocument = JsonDocument.Parse(first.Out);
        var next = firstDocument.RootElement.GetProperty("next").GetInt64();
        var second = await _cli.Run("events", "--after", next.ToString(System.Globalization.CultureInfo.InvariantCulture), "--json");
        using var secondDocument = JsonDocument.Parse(second.Out);
        var empty = await _cli.Run("events", "--after", "3", "--json");
        using var emptyDocument = JsonDocument.Parse(empty.Out);

        Assert.Equal(0, first.Exit);
        Assert.Equal(2, next);
        Assert.Equal(2, firstDocument.RootElement.GetProperty("events").GetArrayLength());
        var third = secondDocument.RootElement.GetProperty("events")[0];
        Assert.Equal("lane.down", third.GetProperty("kind").GetString());
        Assert.Equal("ui", third.GetProperty("lane").GetString());
        Assert.Equal("idle", third.GetProperty("fields").GetProperty("reason").GetString());
        Assert.Equal(3, secondDocument.RootElement.GetProperty("next").GetInt64());
        Assert.Equal(0, emptyDocument.RootElement.GetProperty("events").GetArrayLength());
        Assert.Equal(3, emptyDocument.RootElement.GetProperty("next").GetInt64());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Events_json_carries_oldest_latest_and_gap_on_an_empty_log_and_on_a_gap()
    {
        var empty = await _cli.Run("events", "--json");
        using var emptyDocument = JsonDocument.Parse(empty.Out);
        await Seed();
        var events = new SqliteMetaEventStore(new MetaDatabase(MetaDatabase.OptionsFor(_cli.File("meta")), _cli.Clock));
        _ = await events.Prune(new DateTimeOffset(2026, 10, 15, 5, 1, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);   // drops event 1
        var gap = await _cli.Run("events", "--json");
        using var gapDocument = JsonDocument.Parse(gap.Out);
        var caughtUp = await _cli.Run("events", "--after", "1", "--json");
        using var caughtUpDocument = JsonDocument.Parse(caughtUp.Out);

        Assert.Equal((1L, 0L, false), (emptyDocument.RootElement.GetProperty("oldest").GetInt64(), emptyDocument.RootElement.GetProperty("latest").GetInt64(), emptyDocument.RootElement.GetProperty("gap").GetBoolean()));
        Assert.Equal((2L, 3L, true), (gapDocument.RootElement.GetProperty("oldest").GetInt64(), gapDocument.RootElement.GetProperty("latest").GetInt64(), gapDocument.RootElement.GetProperty("gap").GetBoolean()));
        Assert.False(caughtUpDocument.RootElement.GetProperty("gap").GetBoolean());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Events_text_warns_on_a_gap()
    {
        await Seed();
        var events = new SqliteMetaEventStore(new MetaDatabase(MetaDatabase.OptionsFor(_cli.File("meta")), _cli.Clock));
        _ = await events.Prune(new DateTimeOffset(2026, 10, 15, 5, 1, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);

        var shown = await _cli.Run("events");

        Assert.Equal(0, shown.Exit);
        Assert.Contains("events before #2 were dropped (7-day retention): re-read status", shown.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Events_filters_by_kind_and_lane()
    {
        await Seed();

        var byKind = await _cli.Run("events", "--kind", "box.health");
        var byLane = await _cli.Run("events", "--lane", "ui");

        Assert.Equal("2 2026-10-15T05:01:00Z box.health - {\"mem_available_mb\":9000}", byKind.Out.TrimEnd());
        Assert.Equal("3 2026-10-15T05:02:00Z lane.down ui {\"lane\":\"ui\",\"reason\":\"idle\"}", byLane.Out.TrimEnd());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("--kind", "Bad.Kind", "AW-META-027")]
    [InlineData("--after", "x", "AW-META-001")]
    [InlineData("--after", "-1", "AW-META-001")]
    [InlineData("--max", "0", "AW-META-001")]
    [InlineData("--max", "many", "AW-META-001")]
    public async Task Events_with_a_bad_option_is_a_usage_error_and_prints_the_code(string option, string value, string code)
    {
        var shown = await _cli.Run("events", option, value);

        Assert.Equal(2, shown.Exit);
        Assert.StartsWith(code + ":", shown.Err, StringComparison.Ordinal);
        Assert.Empty(shown.Out);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Events_never_writes_so_an_empty_log_prints_nothing()
    {
        var shown = await _cli.Run("events");

        Assert.Equal(0, shown.Exit);
        Assert.Empty(shown.Out);
    }

    public void Dispose() => _cli.Dispose();
}
