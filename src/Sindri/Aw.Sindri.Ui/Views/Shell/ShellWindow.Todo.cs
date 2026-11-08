using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus on Home's Todo panel (<see cref="ShellWindow"/>, continued; todo spec §3.3): G T lands on the first row, or on the panel when it has none.</summary>
internal sealed partial class ShellWindow
{
    // The first drawn todo row's frame takes focus on the keyboard's ring; with no row (loading, empty) the panel's own frame does.
    private void FocusTodoPanel()
    {
        var frame = this.Get<Border>("Page").GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => string.Equals(b.Name, "TodoPanelFrame", StringComparison.Ordinal) && b.IsEffectivelyVisible);
        var row = frame?.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("item-row") && b.Focusable && b.IsEffectivelyVisible);
        if (Usable(row ?? frame) is not { } target)
        {
            KeepFocus();   // Home is not drawn: focus still goes somewhere
            return;
        }

        _ = target.Focus(NavigationMethod.Tab);
    }
}
