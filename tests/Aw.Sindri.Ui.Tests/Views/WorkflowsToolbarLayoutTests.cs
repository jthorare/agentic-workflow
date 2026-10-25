using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The Workflows page's toolbar at the 900 px minimum (#1073): no empty band between it and the list, and the key hints stay inside it.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsToolbarLayoutTests : FrameHost
{
    private const double PanelGap = 16;

    [Fact]
    public Task The_list_follows_the_toolbar_by_one_panel_gap() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0, width: 900);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);
        Settle(window);

        var toolbar = Named<WrapPanel>(window, "WorkflowsToolbar");
        var list = Named<Control>(window, "WorkflowsList");
        var gap = list.TranslatePoint(default, window)!.Value.Y - (toolbar.TranslatePoint(default, window)!.Value.Y + toolbar.Bounds.Height);
        Assert.True(gap < PanelGap + 8, $"{gap} px sit between the toolbar and the list (one panel gap is {PanelGap})");
    });

    [Fact]
    public Task The_key_hints_end_inside_the_toolbar() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0, width: 900);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);
        Settle(window);

        var toolbar = Named<WrapPanel>(window, "WorkflowsToolbar");
        var hints = Named<TextBlock>(window, "WorkflowsKeyHints");
        var right = hints.TranslatePoint(default, window)!.Value.X + hints.Bounds.Width;
        var edge = toolbar.TranslatePoint(default, window)!.Value.X + toolbar.Bounds.Width;
        Assert.True(right <= edge + 1, $"the key hints end at {right}, past the toolbar's edge {edge}");
    });
}
