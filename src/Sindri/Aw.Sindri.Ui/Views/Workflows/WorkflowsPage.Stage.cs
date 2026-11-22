using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Workflows page, continued (#3684): the stages are sized from the scroll viewer's viewport.</summary>
internal sealed partial class WorkflowsPage
{
    private ScrollViewer? _pages;

    // The page scrolls, so the stages are sized from the scroll viewer's viewport: they fill the window under the header and the step details sit below (#3684).
    private void OnPagesProperty(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty || e.Property == BoundsProperty)
        {
            SizeStages();
        }
    }

    private void SizeStages()
    {
        if (_pages is null)
        {
            return;
        }

        foreach (var panel in new[] { RunsStagePanel, RunPanel })
        {
            // the top inside the scroll viewer's content, not inside this page: the page sits under the shell's 24 px gutter, so the page's own top would leave the stage 24 px too tall
            if (panel.IsVisible && panel.TranslatePoint(default, _pages) is { } at)
            {
                var wanted = GraphZoom.StageHeight(_pages.Viewport.Height, at.Y + _pages.Offset.Y, 16);
                if (Math.Abs(panel.Height - wanted) > 0.5)
                {
                    panel.Height = wanted;
                }
            }
        }
    }
}
