using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox's groups (needs-you spec §4): the served rows told apart by id prefix and kind, in the page's order; the goldens decide.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouGroupingTests
{
    private static readonly HomeCounts _counts = new(Running: 0, WithYou: 0, Queued: 0);

    private static IReadOnlyList<NeedsYouEntry> Golden(string scenario)
    {
        var list = AwJson.Deserialize<NeedsYouList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("needs-you", scenario)), SindriApiClient.Json).Value;
        var catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;
        return NeedsYouMapper.Map(list, catalog, decisions: null, _counts).Rows;
    }

    private static (NeedsYouGroupKind Kind, string Heading, int Count)[] Shape(IEnumerable<NeedsYouEntry> rows) =>
        [.. NeedsYouGrouping.Split(rows, e => e).Select(g => (g.Kind, g.Heading, g.Rows.Count))];

    [Fact]
    public void The_fixture_has_decisions_then_settings_then_the_failure()
    {
        Assert.Equal(
            [(NeedsYouGroupKind.Decisions, "Agent decisions", 1), (NeedsYouGroupKind.Settings, "Workflow and settings approvals", 1), (NeedsYouGroupKind.Failures, "Something failed", 1)],
            Shape(Golden("fixture")));
    }

    [Fact]
    public void The_large_list_counts_each_group()
    {
        Assert.Equal(
            [(NeedsYouGroupKind.Decisions, "Agent decisions", 12), (NeedsYouGroupKind.Settings, "Workflow and settings approvals", 1), (NeedsYouGroupKind.Failures, "Something failed", 1)],
            Shape(Golden("large")));
    }

    [Fact]
    public void A_desk_task_is_your_approval() =>
        Assert.Equal([(NeedsYouGroupKind.Approvals, "Your approvals", 1)], Shape(Golden("desk-task")));

    [Fact]
    public void A_parked_run_is_a_paused_run() =>
        Assert.Equal([(NeedsYouGroupKind.Runs, "Paused runs", 1)], Shape(Golden("park")));

    [Fact]
    public void A_run_you_took_over_is_a_paused_run() =>
        Assert.Equal([(NeedsYouGroupKind.Runs, "Paused runs", 1)], Shape(Golden("idle-takeover")));

    [Fact]
    public void An_empty_list_has_no_groups() =>
        Assert.Empty(Shape([]));

    [Fact]
    public void Groups_come_in_the_pages_order_and_rows_keep_the_served_order_inside_each()
    {
        var rows = new[]
        {
            NeedsYouRows.Failure(),
            NeedsYouRows.Decision("decision:DEC-2"),
            NeedsYouRows.Park("takeover:WFI-1"),
            NeedsYouRows.Profile(),
            NeedsYouRows.Park("desk:WFI-2"),
            NeedsYouRows.Decision("decision:DEC-1"),
            NeedsYouRows.Park("park:WFI-3"),
            NeedsYouRows.Park("setup:recipes"),
        };

        var groups = NeedsYouGrouping.Split(rows, e => e);

        Assert.Equal([NeedsYouGroupKind.Approvals, NeedsYouGroupKind.Runs, NeedsYouGroupKind.Decisions, NeedsYouGroupKind.Settings, NeedsYouGroupKind.Failures], groups.Select(g => g.Kind));
        Assert.Equal(
            [["desk:WFI-2"], ["takeover:WFI-1", "park:WFI-3"], ["decision:DEC-2", "decision:DEC-1"], ["profile:drift", "setup:recipes"], ["failure:service:index"]],
            groups.Select(g => g.Rows.Select(r => r.Id).ToArray()));
    }

    [Fact]
    public void A_row_the_app_does_not_know_gets_a_group_headed_by_its_served_kind()
    {
        var odd = NeedsYouRows.Park("novel:1") with { Kind = "novel" };

        var group = Assert.Single(NeedsYouGrouping.Split([odd], e => e));

        Assert.Equal((NeedsYouGroupKind.Other, "novel"), (group.Kind, group.Heading));
    }

    [Fact]
    public void A_row_is_a_decision_by_its_subject_even_under_an_id_the_app_does_not_know()
    {
        var odd = NeedsYouRows.Decision("odd:1");

        Assert.Equal(NeedsYouGroupKind.Decisions, NeedsYouGrouping.KindOf(odd));
    }
}
