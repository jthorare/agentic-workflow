using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3683 frame wf-compare at 1440 and 1000: two ticked runs side by side, each step with both runs' status and output, a differing line marked with a bar and the word "differs", the summary in the header, and Esc back to the runs.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsCompareFramesTests : FrameHost
{
    private const string RunA = "morning-check@2:5";
    private const string RunB = "morning-check@1:4";

    private static JsonObject Step(string node, string status, JsonNode? output = null, string? error = null) =>
        new() { ["node"] = node, ["itemIndex"] = 0, ["status"] = status, ["input"] = null, ["output"] = output, ["truncated"] = false, ["error"] = error, ["agent"] = null, ["raw"] = null, ["seconds"] = 0.4 };

    private JsonObject Run(string id, int revision, TimeSpan ago, string by, string status) => new()
    {
        ["id"] = id, ["slug"] = Slug, ["revision"] = revision, ["status"] = status, ["trigger"] = new JsonObject { ["kind"] = "manual" }, ["outputs"] = null, ["error"] = null, ["itemId"] = null,
        ["startedAt"] = (Clock.GetUtcNow() - ago).ToString("O", System.Globalization.CultureInfo.InvariantCulture), ["endedAt"] = (Clock.GetUtcNow() - ago).AddSeconds(10).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        ["nodes"] = new JsonArray(Step("start", "succeeded", new JsonObject { ["payload"] = new JsonObject { ["by"] = by } }), Step("sum", status, new JsonObject { ["result"] = 3 })),
    };

    private async Task<(Window Window, ShellViewModel Shell)> Compared(double width)
    {
        var row = (string id, int rev, TimeSpan ago) => new JsonObject { ["id"] = id, ["revision"] = rev, ["status"] = "succeeded", ["trigger"] = "manual", ["startedAt"] = (Clock.GetUtcNow() - ago).ToString("O", System.Globalization.CultureInfo.InvariantCulture), ["endedAt"] = (Clock.GetUtcNow() - ago).ToString("O", System.Globalization.CultureInfo.InvariantCulture), ["error"] = null };
        Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, new JsonObject { ["slug"] = Slug, ["runs"] = new JsonArray(row(RunA, 2, TimeSpan.FromHours(2)), row(RunB, 1, TimeSpan.FromHours(30))) }.ToJsonString());
        Server.WorkflowRunAnswers[RunA] = new RouteAnswer(System.Net.HttpStatusCode.OK, Run(RunA, 2, TimeSpan.FromHours(2), "human:cli", "succeeded").ToJsonString());
        Server.WorkflowRunAnswers[RunB] = new RouteAnswer(System.Net.HttpStatusCode.OK, Run(RunB, 1, TimeSpan.FromHours(30), "human:ui", "succeeded").ToJsonString());
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count == 2, "the runs never showed");
        foreach (var run in shell.Workflows.Runs.Rows)
        {
            run.IsSelected = true;
        }

        await shell.Workflows.OpenCompare(CancellationToken.None);
        await Until(window, () => Visible(window, "CompareSteps"), "the Compare view never showed");
        Settle(window);
        return (window, shell);
    }

    private static List<string> Visibles(Window window, string name) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == name && t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    [Theory]
    [InlineData(1440)]
    [InlineData(1000)]
    public Task Compare_shows_both_runs_each_step_and_marks_the_differing_line(double width) => OnUi(async () =>
    {
        var (window, _) = await Compared(width);

        Assert.Equal(2, Visibles(window, "CompareRunTitle").Count);
        Assert.StartsWith("Run A", Visibles(window, "CompareRunTitle")[0], StringComparison.Ordinal);
        Assert.Equal(["start", "sum"], Visibles(window, "CompareStepName"), StringComparer.Ordinal);
        Assert.Equal(2, Visibles(window, "CompareDiffers").Count);   // the "by" line, once in each run's cell
        Assert.Equal("differs in 1 of 2 steps", Named<TextBlock>(window, "CompareSummary").Text);
        var steps = Named<ItemsControl>(window, "CompareSteps");
        Assert.True(steps.Bounds.Width <= window.Bounds.Width, "the comparison is wider than the window");
    });

    [Fact]
    public Task Esc_goes_back_to_the_runs() => OnUi(async () =>
    {
        var (window, shell) = await Compared(1440);

        _ = Named<Button>(window, "WorkflowsBack").Focus();   // a level opens with focus on its Back button
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        await Until(window, () => shell.Workflows.IsRuns, "Esc never went back to the runs");
    });
}
