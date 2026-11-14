using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3683 frame wf-runs at 1440 and 1000: the runs table has Time, Trigger, Revision, Duration and Result columns and no run id, a status and a date filter with the count, a tick box per run, and Compare 2 once two are ticked.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsRunsTableFramesTests : FrameHost
{
    private JsonObject Run(int n, string status, TimeSpan ago, string trigger)
    {
        var started = Clock.GetUtcNow() - ago;
        return new JsonObject
        {
            ["id"] = $"morning-check@2:{n}", ["revision"] = 2, ["status"] = status, ["trigger"] = trigger,
            ["startedAt"] = started.ToString("O", System.Globalization.CultureInfo.InvariantCulture), ["endedAt"] = started.AddSeconds(10).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["error"] = status == "failed" ? "boom" : null, ["seconds"] = 10, ["result"] = status == "failed" ? "tell failed: the notification was not delivered" : "Sum is 3",
        };
    }

    private async Task<(Window Window, ShellViewModel Shell)> Opened(double width)
    {
        Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, new JsonObject
        {
            ["slug"] = Slug,
            ["runs"] = new JsonArray(Run(3, "failed", TimeSpan.FromMinutes(22), "schedule"), Run(2, "succeeded", TimeSpan.FromHours(2), "cli"), Run(1, "failed", TimeSpan.FromHours(7), "manual")),
        }.ToJsonString());
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count == 3 && Visible(window, "RunRows"), "the runs never showed");
        Settle(window);
        return (window, shell);
    }

    private static List<string> Visibles(Window window, string name) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == name && t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    [Theory]
    [InlineData(1440)]
    [InlineData(1000)]
    public Task The_table_has_its_columns_and_cells_and_never_a_run_id(double width) => OnUi(async () =>
    {
        var (window, _) = await Opened(width);

        Assert.Equal(["Time", "Trigger", "Revision", "Duration", "Result"], Visibles(window, "RunColumnHead"), StringComparer.Ordinal);
        Assert.Equal(["22m ago", "2h ago", "7h ago"], Visibles(window, "RunTime"), StringComparer.Ordinal);
        Assert.Equal(["On schedule", "From the CLI", "By hand"], Visibles(window, "RunTrigger"), StringComparer.Ordinal);
        Assert.Equal(["r2", "r2", "r2"], Visibles(window, "RunRevision"), StringComparer.Ordinal);
        Assert.Equal(["10 s", "10 s", "10 s"], Visibles(window, "RunDuration"), StringComparer.Ordinal);
        Assert.Contains("tell failed: the notification was not delivered", Visibles(window, "RunResult"));
        var rows = Named<ItemsControl>(window, "RunRows");
        Assert.DoesNotContain(rows.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && (t.Text ?? string.Empty).Contains("morning-check@", StringComparison.Ordinal));
        Assert.True(rows.Bounds.Width <= window.Bounds.Width, "the table is wider than the window");
    });

    [Fact]
    public Task The_filters_narrow_the_rows_and_the_count_follows() => OnUi(async () =>
    {
        var (window, shell) = await Opened(1440);
        Assert.Equal("3 runs", Named<TextBlock>(window, "RunsCount").Text);

        shell.Workflows.Runs.SetStatusFilter("failed");
        await Until(window, () => Named<TextBlock>(window, "RunsCount").Text == "2 of 3 runs", "the count never followed the filter");

        Assert.True(Visible(window, "RunsClearFilters"));
        Assert.Equal(2, Visibles(window, "RunTime").Count);
    });

    [Fact]
    public Task Ticking_two_runs_shows_Compare_2() => OnUi(async () =>
    {
        var (window, shell) = await Opened(1440);
        Assert.False(Visible(window, "RunsCompare"));

        var ticks = Named<ItemsControl>(window, "RunRows").GetVisualDescendants().OfType<CheckBox>().Where(c => c.Name == "RunTick").ToList();
        ticks[0].IsChecked = true;
        ticks[2].IsChecked = true;

        await Until(window, () => Visible(window, "RunsCompare"), "Compare never showed");
        Assert.Equal("Compare 2", Named<Button>(window, "RunsCompare").Content);
        Assert.Equal(2, shell.Workflows.Runs.SelectedCount);
    });
}
