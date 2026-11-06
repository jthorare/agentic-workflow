using System.Net;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Facts;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Facts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>U8 key frames (UI spec §18.2) for the facts panel in the instance frame, against the in-memory API serving the instance and fact goldens. Saved under frames/ as u8-frame-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class FactsFramesTests : FrameHost
{
    private async Task<(Window Window, ShellViewModel Shell)> Opened(string scenario)
    {
        var detail = FixtureInstances.Detail(scenario);
        var (window, shell) = await Connected(width: 1600, height: 1000);
        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Facts is not null);
        Settle(window);
        OpenDisclosures(window);
        return (window, shell);
    }

    private static void Frame(Window window, string name) => Frame(window, name, "u8-frame-");

    private static Button ViewButton(Window window, FactRowViewModel row) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "FactView" && Avalonia.Automation.AutomationProperties.GetName(b) == row.ViewName);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public Task u8_frame_facts_payload_open() => OnUi(async () =>
    {
        var (window, shell) = await Opened("incident-loop");
        var alert = shell.Instance.Facts!.Rows.Single(r => r.Key == "fact:alert");
        var button = ViewButton(window, alert);
        Assert.True(button.Focus(NavigationMethod.Tab));

        Click(button);
        await Until(window, () => alert.Viewer is PayloadFactViewer { IsLoading: false });

        _ = Assert.IsType<PayloadFactViewer>(alert.Viewer);
        Assert.Equal(FixtureInstances.Detail("incident-loop").Facts.Count, window.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Name == "FactLabel"));
        Assert.False(Visible(window, "PayloadScalar"));   // the body is drawn once, as the tree
        Assert.True(Visible(window, "PayloadTree"));
        Assert.Same(button, window.FocusManager?.GetFocusedElement());
        Assert.Equal(Labels.HideFact, button.Content?.ToString());
        Frame(window, "facts-payload-open");
    });

    [Fact]
    public Task u13_frame_facts_scope_map_table() => OnUi(async () =>
    {
        var (window, shell) = await Opened("project-build");
        var scope = shell.Instance.Facts!.Rows.Single(r => r.Key == "fact:scope");
        Click(ViewButton(window, scope));
        await Until(window, () => scope.Viewer is ScopeMapViewer { IsLoading: false } && Visible(window, "ScopeCounts"));

        var viewer = (ScopeMapViewer)scope.Viewer!;
        Assert.Equal(viewer.Counts, Named<TextBlock>(window, "ScopeCounts").Text);
        var table = Named<StackPanel>(window, "ScopeTable");
        var heard = Heard.Tree(table).Select(h => h.Name).ToList();
        Assert.Contains(heard, h => string.Equals(h, viewer.Counts, StringComparison.Ordinal));
        Assert.All(viewer.Surfaces, s => Assert.Contains(heard, h => string.Equals(h, s.Title, StringComparison.Ordinal)));
        Assert.All(viewer.Surfaces, s => Assert.Contains(heard, h => string.Equals(h, s.Chip, StringComparison.Ordinal)));
        Assert.False(Visible(window, "ScopeTree"));
        Click(Named<Button>(window, "ScopeToggle"));
        await Until(window, () => Visible(window, "ScopeTree"));
        Assert.False(Visible(window, "ScopeCounts"));
        Click(Named<Button>(window, "ScopeToggle"));
        await Until(window, () => Visible(window, "ScopeCounts"));
        Frame(window, "facts-scope-map");
    });

    [Fact]
    public Task Text_copy_feedback_is_a_polite_live_region() => OnUi(async () =>
    {
        var (window, shell) = await Opened("incident-loop");
        var severity = shell.Instance.Facts!.Rows.Single(r => r.Key == "fact:severity");
        Click(ViewButton(window, severity));
        await Until(window, () => severity.Viewer is TextFactViewer && Visible(window, "FactTextCopy"));
        var viewer = (TextFactViewer)severity.Viewer!;

        Click(Named<Button>(window, "FactTextCopy"));
        await Until(window, () => viewer.Copying.IsAnnouncing);

        var region = Named<TextBlock>(window, "FactTextAnnouncement");
        Assert.Equal((Labels.Copied, true), (region.Text, region.Classes.Contains("live")));
        Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Polite, Avalonia.Automation.AutomationProperties.GetLiveSetting(region));
    });

    [Fact]
    public Task A_pending_desk_output_shows_awaiting_and_its_cli_without_a_View_button() => OnUi(async () =>
    {
        var (window, shell) = await Opened("dev-desk-round-trip");
        var pending = shell.Instance.Facts!.Rows.Single(r => r.IsAwaiting);

        Assert.Equal(pending.PendingCli, window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "FactPendingCli" && t.IsEffectivelyVisible).Text);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "FactView" && b.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(b) == pending.ViewName);
        Frame(window, "facts-pending-desk");
    });

    [Fact]
    public Task A_violation_trace_is_mono_and_each_step_reads_its_own_text() => OnUi(async () =>
    {
        // The hand test's two bugs (#2135): TextBlock.mono missed the SelectableTextBlock trace, and the step items had no name.
        const string detail = "1. Start: remaining = 3\n2. Elapse: remaining 3 -> 2\n\nFull trace:\nState 1: <Initial predicate>\n/\\ remaining = 3\n\nState 2: <Elapse>\n/\\ remaining = 2";
        var body = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["rule"] = "MDL-VIOLATION-001", ["message"] = "The timer can run while paused", ["detail"] = detail });
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"key":"fact:project","type":"findings","body":{{body.ToJsonString()}},"truncated":false}""");
        var row = FixtureInstances.Detail("project-build").Facts.Single(f => f.Key == "fact:project") with { Type = FactType.Findings };
        var viewer = new FindingsFactViewer(row, new FactViewerContext(Server, new FakeClipboard(), new FakeTimeProvider(A.Noon), static () => new Uri("http://127.0.0.1:7410/"), row.Key, static _ => { }));
        await viewer.Load(Token);
        viewer.Findings[0].IsTraceOpen = true;
        var window = new Window { Width = 900, Height = 700, Content = new FindingsFactView { DataContext = viewer } };
        window.Show();
        Settle(window);

        var trace = window.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "FindingTrace");
        var mono = (FontFamily)window.FindResource("Font.Mono")!;
        string?[] names = [.. window.GetVisualDescendants().OfType<ListBoxItem>().Select(static i => Avalonia.Automation.AutomationProperties.GetName(i))];
        Assert.Equal((mono, Labels.FullTrace), (trace.FontFamily, Avalonia.Automation.AutomationProperties.GetName(trace)));
        Assert.Equal(["1. Start: remaining = 3", "2. Elapse: remaining 3 -> 2"], names, StringComparer.Ordinal);
        window.Close();
    });
}
