using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3683 list frames (wf-list at 1440, wf-list-1000): a header row, the "Needs a look" and "Everything else" groups, each row with its trigger, last run chip, next run, a 10-slot strip and state, and the short id only beside a name two workflows share.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsListFramesTests : FrameHost
{
    private string At(TimeSpan before) => (Clock.GetUtcNow() - before).ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private JsonObject Row(string slug, string name, int? active = 1, string? last = null, TimeSpan? ago = null, string trigger = "By hand", bool? paused = null, string? next = null, int bars = 0)
    {
        var row = new JsonObject { ["slug"] = slug, ["name"] = name, ["activeRevision"] = active, ["latestRevision"] = 1, ["changedBy"] = "owner", ["changedAt"] = At(TimeSpan.FromDays(3)), ["trigger"] = trigger, ["paused"] = paused, ["nextRunAt"] = next };
        if (last is not null)
        {
            row["lastRun"] = new JsonObject { ["run"] = slug + "@1:9", ["status"] = last, ["at"] = At(ago ?? TimeSpan.FromMinutes(3)) };
        }

        var recent = new JsonArray();
        for (var i = 0; i < bars; i++)
        {
            recent.Add(new JsonObject { ["run"] = $"{slug}@1:{bars - i}", ["status"] = i == 0 && last is not null ? last : "succeeded", ["startedAt"] = At(TimeSpan.FromMinutes(20 * (i + 1))), ["seconds"] = 10 });
        }

        row["recent"] = recent;
        return row;
    }

    private async Task<(Window Window, ShellViewModel Shell)> Shown(double width)
    {
        Server.WorkflowsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, new JsonObject
        {
            ["workflows"] = new JsonArray(
                Row("flaky", "Flaky checker", last: "failed", bars: 10),
                Row("gate", "Nightly main gate", trigger: "Every night 2:00", paused: true),
                Row("sum", "Sum numbers", trigger: "Every 20 min", last: "succeeded", ago: TimeSpan.FromMinutes(2), next: At(-TimeSpan.FromMinutes(18)), bars: 3),
                Row("hello-sindri", "hello-sindri"),
                Row("tpl-hello-sindri", "hello-sindri", active: null)),
        }.ToJsonString());
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count == 5 && Visible(window, "WorkflowRows"), "the list never showed its rows");
        Settle(window);
        return (window, shell);
    }

    private static List<Border> RowBorders(Window window) => [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "WorkflowRow")];

    private static IEnumerable<string> Texts(Window window, string name) =>
        window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == name && t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty);

    [Fact]
    public Task At_1440_the_list_has_every_column_the_groups_and_a_ten_slot_strip_per_row() => OnUi(async () =>
    {
        var (window, _) = await Shown(1440);

        Assert.Equal(["Name", "Trigger", "Last run", "Next run", "Last 10 runs", "State"], Texts(window, "ColumnHead").ToList(), StringComparer.Ordinal);
        Assert.Equal(["Needs a look · 2", "Everything else · 3"], Texts(window, "RowGroupHeader").ToList(), StringComparer.Ordinal);
        var rows = RowBorders(window);
        Assert.Equal(5, rows.Count);
        Assert.All(rows, row => Assert.Equal(10, row.GetVisualDescendants().OfType<Border>().Count(b => b.Name == "RunBar")));
        var flaky = rows.First(r => ((WorkflowRowViewModel)r.DataContext!).Slug == "flaky");
        var chip = flaky.GetVisualDescendants().OfType<StateChipView>().Single(c => c.Name == "RowLastRun");
        Assert.StartsWith("Failed ", chip.Word, StringComparison.Ordinal);
        var tall = flaky.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "RunBar").Max(b => b.Bounds.Height);
        Assert.Equal(16, tall);   // a failed run is the tall bar
    });

    [Fact]
    public Task The_short_id_shows_only_beside_a_name_two_workflows_share() => OnUi(async () =>
    {
        var (window, _) = await Shown(1440);

        Assert.Equal(["hello-sindri", "tpl-hello-sindri"], Texts(window, "RowId").Order(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
    });

    [Fact]
    public Task A_paused_workflow_shows_Paused_in_State_and_in_Last_run() => OnUi(async () =>
    {
        var (window, _) = await Shown(1440);

        var gate = RowBorders(window).Single(r => ((WorkflowRowViewModel)r.DataContext!).Slug == "gate");
        Assert.Equal("Paused", gate.GetVisualDescendants().OfType<StateChipView>().Single(c => c.Name == "RowLastRun").Word);
        Assert.Contains(gate.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "RowState" && t.Text == "Paused");
    });
}
