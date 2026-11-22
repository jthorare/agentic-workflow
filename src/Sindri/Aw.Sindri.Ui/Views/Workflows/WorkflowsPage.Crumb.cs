using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Workflows page, continued: the crumb follows the title's baseline (#2807), aligned once per change of its text, the title or the width (#3655).</summary>
internal sealed partial class WorkflowsPage
{
    // What the crumb was last aligned for: its words, the title's words and size (the text scale), and the page's width. The walk for the crumb's TextBlock runs once, not on every layout.
    private (string? Crumb, string? Title, double Width, double Size)? _aligned;
    private TextBlock? _crumbText;

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        SizeStages();
        if (!WorkflowsBack.IsVisible)
        {
            _aligned = null;
            return;
        }

        var key = (WorkflowsBack.Content as string, WorkflowsTitle.Text, Bounds.Width, WorkflowsTitle.FontSize);
        if (_aligned == key)
        {
            return;
        }

        if (_crumbText is not { IsAttachedToVisualTree: true })
        {
            _crumbText = WorkflowsBack.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
        }

        var moved = AlignCrumb(WorkflowsBack, _crumbText);
        moved |= AlignCrumb(WorkflowsCrumbSep, WorkflowsCrumbSep);
        _aligned = moved ? null : key;   // a part that moved changes the layout: look again on the next pass, and stop once it settles
    }

    // Moves a top-aligned crumb part down by the gap between its text's baseline and the page title's (#2807).
    // Under half a pixel is left alone, so the layout it causes settles on the next pass.
    private bool AlignCrumb(Control part, TextBlock? text)
    {
        if (text is null || text.TranslatePoint(default, this) is not { } at || WorkflowsTitle.TranslatePoint(default, this) is not { } title)
        {
            return false;
        }

        var gap = title.Y + WorkflowsTitle.TextLayout.Baseline - (at.Y + text.TextLayout.Baseline);
        if (Math.Abs(gap) < 0.5)
        {
            return false;
        }

        var m = part.Margin;
        part.Margin = new Thickness(m.Left, m.Top + gap, m.Right, m.Bottom);
        return true;
    }
}
