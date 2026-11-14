using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3683 list row (frames wf-list, wf-list-1000): each row says its trigger in words, its last run with its age, its next run, the last 10 runs as a strip and its state; failed and paused workflows come first under
/// "Needs a look", the rest by latest activity, and a name two workflows share shows the short id beside it. The clock is 2026-01-01 00:05 UTC.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowListRowTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string At(TimeSpan before) => (Now - before).ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static JsonObject Row(string slug, string name, int? active = 1, string? lastStatus = null, TimeSpan? lastAgo = null, string? trigger = "By hand", bool? paused = null, string? nextAt = null, JsonArray? recent = null)
    {
        var row = new JsonObject
        {
            ["slug"] = slug,
            ["name"] = name,
            ["activeRevision"] = active,
            ["latestRevision"] = 1,
            ["changedBy"] = "owner",
            ["changedAt"] = At(TimeSpan.FromDays(3)),
            ["trigger"] = trigger,
            ["paused"] = paused,
            ["nextRunAt"] = nextAt,
        };
        if (lastStatus is not null)
        {
            row["lastRun"] = new JsonObject { ["run"] = slug + "@1:9", ["status"] = lastStatus, ["at"] = At(lastAgo ?? TimeSpan.FromMinutes(3)) };
        }

        row["recent"] = recent;
        return row;
    }

    private async Task<Aw.Sindri.Ui.ViewModels.Workflows.WorkflowsPageViewModel> Serve(params JsonObject[] rows)
    {
        _f.ServeList(new JsonObject { ["workflows"] = new JsonArray([.. rows.Select(r => (JsonNode?)r)]) });
        return await _f.Shown(Token);
    }

    [Fact]
    public async Task A_row_says_its_trigger_and_its_last_run_with_the_age_and_a_tone()
    {
        var page = await Serve(
            Row("flaky", "Flaky checker", lastStatus: "failed", lastAgo: TimeSpan.FromMinutes(3)),
            Row("sum", "Sum numbers", trigger: "Every 20 min", lastStatus: "succeeded", lastAgo: TimeSpan.FromMinutes(2)),
            Row("slow", "Slow nudge", lastStatus: "cancelled", lastAgo: TimeSpan.FromMinutes(6)),
            Row("fresh", "Fresh one"));

        var flaky = page.Rows.Single(r => r.Slug == "flaky");
        var sum = page.Rows.Single(r => r.Slug == "sum");
        var slow = page.Rows.Single(r => r.Slug == "slow");
        var fresh = page.Rows.Single(r => r.Slug == "fresh");
        Assert.Equal(("By hand", "Failed 3m ago", "error", true), (flaky.TriggerText, flaky.LastRunText, flaky.LastRunToneWord, flaky.ShowsLastRun));
        Assert.Equal(("Every 20 min", "Succeeded 2m ago", "ok"), (sum.TriggerText, sum.LastRunText, sum.LastRunToneWord));
        Assert.Equal(("Cancelled 6m ago", "neutral"), (slow.LastRunText, slow.LastRunToneWord));
        Assert.Equal((string.Empty, false), (fresh.LastRunText, fresh.ShowsLastRun));
    }

    [Fact]
    public async Task A_paused_workflow_reads_Paused_in_State_and_in_Last_run_and_is_not_Off()
    {
        var page = await Serve(
            Row("gate", "Nightly main gate", trigger: "Every night 2:00", paused: true, nextAt: At(-TimeSpan.FromHours(2))),
            Row("digest", "Daily digest", active: null, trigger: "Every day 8:00"));

        var gate = page.Rows.Single(r => r.Slug == "gate");
        var digest = page.Rows.Single(r => r.Slug == "digest");
        Assert.Equal((true, "Paused", "Paused", true, "-"), (gate.IsPaused, gate.StateText, gate.LastRunText, gate.ShowsLastRun, gate.NextRunText));
        Assert.Equal((false, "Off", false), (digest.IsPaused, digest.StateText, digest.NeedsLook));   // never turned on: not flagged
    }

    [Theory]
    [InlineData(2, "2:05")]    // later today
    [InlineData(31, "Fri 7:05")]   // another day: its weekday and time
    public async Task The_next_run_reads_as_a_time_today_and_as_a_weekday_and_time_on_another_day(int hours, string expected)
    {
        var page = await Serve(Row("sum", "Sum numbers", trigger: "Every 20 min", nextAt: At(-TimeSpan.FromHours(hours))));

        Assert.Equal(expected, page.Rows[0].NextRunText);
    }

    [Fact]
    public async Task The_strip_holds_ten_slots_oldest_first_with_a_tall_failed_bar_and_a_hollow_cancelled_one()
    {
        var recent = new JsonArray(   // newest first, as served
            Bar("sum@1:3", "failed", TimeSpan.FromMinutes(22), 10),
            Bar("sum@1:2", "cancelled", TimeSpan.FromMinutes(40), 4),
            Bar("sum@1:1", "succeeded", TimeSpan.FromMinutes(60), 9));
        var page = await Serve(Row("sum", "Sum numbers", recent: recent));

        var strip = page.Rows[0].Strip;
        Assert.Equal(10, strip.Count);
        Assert.Equal(["empty", "empty", "empty", "empty", "empty", "empty", "empty", "succeeded", "cancelled", "failed"], strip.Select(b => b.Kind), StringComparer.Ordinal);
        Assert.Equal((false, true, true), (strip[7].IsTall, strip[9].IsTall, strip[8].IsHollow));
        Assert.Equal(("Run 3 · failed · 22m ago", "Wed 31 Dec 2025 · 23:43:00 · 10 s"), (strip[9].TitleLine, strip[9].DetailLine));
    }

    private static JsonObject Bar(string run, string status, TimeSpan ago, double seconds) =>
        new() { ["run"] = run, ["status"] = status, ["startedAt"] = At(ago), ["seconds"] = seconds };

    [Fact]
    public async Task Failed_and_paused_workflows_come_first_under_Needs_a_look_then_the_rest_by_latest_activity()
    {
        var page = await Serve(
            Row("old", "Old one", lastStatus: "succeeded", lastAgo: TimeSpan.FromDays(1)),
            Row("flaky", "Flaky checker", lastStatus: "failed", lastAgo: TimeSpan.FromMinutes(3)),
            Row("new", "New one", lastStatus: "succeeded", lastAgo: TimeSpan.FromMinutes(2)),
            Row("gate", "Nightly main gate", paused: true),
            Row("off", "Off one", active: null));

        Assert.Equal(["flaky", "gate", "new", "old", "off"], page.Rows.Select(r => r.Slug), StringComparer.Ordinal);
        Assert.Equal(("Needs a look · 2", string.Empty, "Everything else · 3", string.Empty), (page.Rows[0].GroupHeader, page.Rows[1].GroupHeader, page.Rows[2].GroupHeader, page.Rows[3].GroupHeader));
        Assert.Equal((true, true, false), (page.Rows[0].NeedsLook, page.Rows[1].NeedsLook, page.Rows[2].NeedsLook));
    }

    [Fact]
    public async Task Two_workflows_with_one_name_show_the_short_id_beside_it_and_others_do_not()
    {
        var page = await Serve(Row("hello-sindri", "hello-sindri"), Row("tpl-hello-sindri", "hello-sindri"), Row("sum", "Sum numbers"));

        Assert.Equal(["hello-sindri", "tpl-hello-sindri"], page.Rows.Where(r => r.ShowsId).Select(r => r.Slug).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
