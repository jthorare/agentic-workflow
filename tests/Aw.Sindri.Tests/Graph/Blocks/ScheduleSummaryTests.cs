using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The Schedule step's summary line (#4254): weekly and daily crons read in words in the presets' voice; any other cron keeps its raw text.</summary>
[Lane(TestLane.Unit)]
public sealed class ScheduleSummaryTests
{
    [Theory]
    [InlineData("0 7 * * 4", "Runs every Thursday at 7:00, UTC")]
    [InlineData("0 7 * * *", "Runs every day at 7:00, UTC")]
    [InlineData("0 7 * * 1,3", "Runs every Monday and Wednesday at 7:00, UTC")]
    [InlineData("30 18 * * 1,3,5", "Runs every Monday, Wednesday and Friday at 18:30, UTC")]
    [InlineData("5 6 * * 1-5", "Runs every weekday at 6:05, UTC")]
    [InlineData("0 7 * * 2-4", "Runs every Tuesday, Wednesday and Thursday at 7:00, UTC")]
    [InlineData("0 7 * * 0", "Runs every Sunday at 7:00, UTC")]
    [InlineData("0 7 * * 7", "Runs every Sunday at 7:00, UTC")]
    [InlineData("0 7 * * 6,0", "Runs every Saturday and Sunday at 7:00, UTC")]
    [InlineData("0 7 * * 0-6", "Runs every day at 7:00, UTC")]
    [InlineData("0 7 * * thu", "Runs every Thursday at 7:00, UTC")]
    [InlineData("0 7 * * MON-FRI", "Runs every weekday at 7:00, UTC")]
    [InlineData("  0 7 * * 4 ", "Runs every Thursday at 7:00, UTC")]
    public void A_weekly_or_daily_cron_reads_in_words(string cron, string expected) =>
        Assert.Equal(expected, Summary(cron));

    [Fact]
    public void A_weekly_cron_keeps_its_time_zone()
    {
        var line = new ScheduleBlock().Summarize(new JsonObject { ["cron"] = "0 7 * * 4", ["timezone"] = "America/Los_Angeles" });

        Assert.Equal("Runs every Thursday at 7:00, America/Los_Angeles", line.Text);
    }

    [Theory]
    [InlineData("*/15 * * * *")]
    [InlineData("*/15 9-17 * * 1-5")]
    [InlineData("0 */2 * * *")]
    [InlineData("30 * * * *")]
    [InlineData("0 7 1 * *")]
    [InlineData("0 7 * 6 4")]
    [InlineData("0 7 * * 1/2")]
    [InlineData("0 7 * * 8")]
    [InlineData("0 7 * * 5-2")]
    [InlineData("60 7 * * 4")]
    [InlineData("0 24 * * 4")]
    [InlineData("0 7 * *")]
    public void A_cron_it_cannot_read_keeps_its_raw_text(string cron) =>
        Assert.Equal($"Runs on schedule {cron.Trim()}, UTC", Summary(cron));

    [Theory]
    [InlineData("0 7 * * 1-5", "Runs every weekday at 7:00, UTC")]
    [InlineData("0 9 * * *", "Runs every day at 9:00, UTC")]
    [InlineData("0 * * * *", "Runs every hour, UTC")]
    [InlineData("0 8 * * 1", "Runs on Mondays at 8:00, UTC")]
    public void The_four_presets_keep_their_words(string cron, string expected) =>
        Assert.Equal(expected, Summary(cron));

    private static string Summary(string cron) => new ScheduleBlock().Summarize(new JsonObject { ["cron"] = cron }).Text;
}
