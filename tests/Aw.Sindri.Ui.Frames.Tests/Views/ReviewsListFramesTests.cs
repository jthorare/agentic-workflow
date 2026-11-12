using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Reviews;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Reviews list in the window (issue #2791): a narrow page reflows instead of overlapping, and J works from the nav tab.</summary>
[Lane(TestLane.Integration)]
public sealed class ReviewsListFramesTests : FrameHost
{
    private async Task<(Window Window, Control Page)> ReviewsOpen(double? pageWidth)
    {
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Reviews);
        await Until(window, () => !shell.Reviews.IsLoading, "the Reviews page never read the list");
        var page = window.GetVisualDescendants().OfType<ReviewsPage>().Single();
        if (pageWidth is { } width)
        {
            page.Width = width;   // what a docked sheet leaves the page
            window.UpdateLayout();
        }

        return (window, page);
    }

    [Fact]
    public Task ReviewsList_columns_do_not_overlap_at_740px() => OnUi(async () =>
    {
        var (window, page) = await ReviewsOpen(pageWidth: 740);

        var texts = page.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && (t.Classes.Contains("subtle") && t.Classes.Contains("caption") && t.Parent is Grid || t.Classes.Contains("mono"))).ToList();

        Assert.NotEmpty(texts);
        Assert.All(texts, t => Assert.True(t.Bounds.Width >= t.DesiredSize.Width - 0.5, $"'{t.Text}' is {t.Bounds.Width:0} px wide but needs {t.DesiredSize.Width:0}"));
        Assert.NotNull(window);
    });

    [Fact]
    public Task Reviews_J_from_nav_tab_focuses_first_row() => OnUi(async () =>
    {
        var (window, _) = await ReviewsOpen(pageWidth: null);
        var tab = window.GetVisualDescendants().OfType<ListBoxItem>().Single(i => string.Equals(Avalonia.Automation.AutomationProperties.GetName(i), "Reviews", StringComparison.Ordinal));
        _ = tab.Focus(NavigationMethod.Tab);

        window.KeyPress(Key.J, RawInputModifiers.None, PhysicalKey.J, keySymbol: "j");

        var rows = window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "ReviewRowFrame" && b.IsEffectivelyVisible).ToList();
        Assert.Same(rows[0], window.FocusManager?.GetFocusedElement());
    });
}
