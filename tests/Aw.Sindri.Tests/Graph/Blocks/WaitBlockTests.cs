using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class WaitBlockTests
{
    private static readonly DateTimeOffset _now = new(2026, 3, 7, 12, 0, 0, TimeSpan.Zero);

    private static JsonObject Config(string key, string value, string? timezone = null)
    {
        var config = new JsonObject { [key] = value };
        if (timezone is not null)
        {
            config["timezone"] = timezone;
        }

        return config;
    }

    [Theory]
    [InlineData("90s", 90)]
    [InlineData("15m", 900)]
    [InlineData("2h", 7200)]
    [InlineData("1d", 86400)]
    [InlineData("1h30m", 5400)]
    public void A_duration_wakes_the_node_that_long_after_now(string duration, double seconds)
    {
        var wake = WaitSchedule.WakeAt(Config("duration", duration), _now);

        Assert.True(wake.IsSuccess);
        Assert.Equal(_now.AddSeconds(seconds), wake.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("0m")]
    [InlineData("15")]
    [InlineData("-5m")]
    public void A_duration_that_is_not_a_positive_length_is_refused(string duration)
    {
        Assert.True(WaitSchedule.WakeAt(Config("duration", duration), _now).IsError);
        Assert.Contains(WaitSchedule.Check(Config("duration", duration)), p => p.Field == "duration");
    }

    [Fact]
    public void A_wait_until_a_time_in_a_timezone_resolves_at_that_instant()
    {
        // 2026-03-08 09:00 in Los Angeles is after the spring-forward at 02:00, so it is PDT (UTC-7), not PST (UTC-8) as it was the day before
        var wake = WaitSchedule.WakeAt(Config("until", "2026-03-08T09:00", "America/Los_Angeles"), _now);

        Assert.Equal(new DateTimeOffset(2026, 3, 8, 16, 0, 0, TimeSpan.Zero), wake.Value);
    }

    [Fact]
    public void A_local_time_the_clock_skips_over_wakes_at_the_first_instant_after_the_gap()
    {
        // 02:30 on 2026-03-08 never happens in Los Angeles: the clock goes from 02:00 PST straight to 03:00 PDT
        var wake = WaitSchedule.WakeAt(Config("until", "2026-03-08T02:30", "America/Los_Angeles"), _now);

        Assert.Equal(new DateTimeOffset(2026, 3, 8, 10, 30, 0, TimeSpan.Zero), wake.Value);   // 03:30 PDT
    }

    [Fact]
    public void A_local_time_that_happens_twice_wakes_at_the_first_one()
    {
        // 01:30 on 2026-11-01 happens in PDT and again an hour later in PST
        var wake = WaitSchedule.WakeAt(Config("until", "2026-11-01T01:30", "America/Los_Angeles"), new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 8, 30, 0, TimeSpan.Zero), wake.Value);   // 01:30 PDT
    }

    [Fact]
    public void A_bare_clock_time_means_its_next_occurrence_in_the_timezone()
    {
        // 12:00Z is 04:00 PST in Los Angeles: 07:00 is still today, 03:00 is tomorrow
        Assert.Equal(new DateTimeOffset(2026, 3, 7, 15, 0, 0, TimeSpan.Zero), WaitSchedule.WakeAt(Config("until", "07:00", "America/Los_Angeles"), _now).Value);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 10, 0, 0, TimeSpan.Zero), WaitSchedule.WakeAt(Config("until", "03:00", "America/Los_Angeles"), _now).Value);   // tomorrow 03:00 is PDT
    }

    [Fact]
    public void A_time_with_an_offset_is_taken_as_written() =>
        Assert.Equal(new DateTimeOffset(2026, 3, 7, 13, 0, 0, TimeSpan.Zero), WaitSchedule.WakeAt(Config("until", "2026-03-07T13:00:00Z"), _now).Value);

    [Fact]
    public void An_until_without_a_timezone_is_read_as_UTC() =>
        Assert.Equal(new DateTimeOffset(2026, 3, 7, 18, 0, 0, TimeSpan.Zero), WaitSchedule.WakeAt(Config("until", "2026-03-07T18:00"), _now).Value);

    [Fact]
    public void An_unknown_timezone_and_a_config_with_both_or_neither_are_refused()
    {
        Assert.Contains(WaitSchedule.Check(Config("until", "09:00", "Mars/Olympus")), p => p.Field == "timezone");
        Assert.Contains(WaitSchedule.Check(new JsonObject { ["duration"] = "5m", ["until"] = "09:00" }), p => p.Field == "until");
        Assert.NotEmpty(WaitSchedule.Check([]));
        Assert.Empty(WaitSchedule.Check(Config("duration", "{{search.delay}}")));   // a reference has no value to check yet
    }

    [Fact]
    public async Task The_wait_block_hands_on_when_it_wakes_and_how_long_it_waits()
    {
        var clock = new FakeTimeProvider(_now);
        var block = new WaitBlock(clock);

        var result = await block.Execute(BlockFakes.Context(Config("duration", "15m")), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["out"], result.Value.Taken);
        Assert.Equal("2026-03-07T12:15:00.0000000+00:00", result.Value.Outputs["wakeAt"]!.GetValue<string>());
        Assert.Equal(900, result.Value.Outputs["waitedSeconds"]!.GetValue<double>());
    }

    [Fact]
    public async Task An_until_in_the_past_wakes_at_once()
    {
        var result = await new WaitBlock(new FakeTimeProvider(_now)).Execute(BlockFakes.Context(Config("until", "2026-03-07T11:00:00Z")), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Value.Outputs["waitedSeconds"]!.GetValue<double>());
    }

    [Fact]
    public void The_wait_block_declares_its_config_outputs_and_a_valid_example()
    {
        IGraphBlockType block = new WaitBlock(new FakeTimeProvider(_now));
        var example = JsonNode.Parse(block.Example)!.AsObject();

        Assert.Equal("wait", block.Type);
        Assert.False(block.IsTrigger);
        Assert.Empty(block.Config.Check(example));
        Assert.Empty(block.CheckConfig(example));
        Assert.Equal(["waitedSeconds", "wakeAt"], block.OutputsFor(example).Fields.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
