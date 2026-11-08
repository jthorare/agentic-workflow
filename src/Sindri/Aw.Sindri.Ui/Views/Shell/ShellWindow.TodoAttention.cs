using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Todo;
using Aw.Sindri.Ui.Views.Todo;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// The Todo panel's attention rows in the window (<see cref="ShellWindow"/>, continued; todo spec §3.3, D13): 1 and 2 pick, Enter opens the line and moves focus onto its first button, Enter on the
/// "Sort out N more" line shows the next row, X, Space and Delete send nothing, and focus follows each change.
/// </summary>
internal sealed partial class ShellWindow
{
    private const string AttentionLineName = "TodoAttentionLine";
    private const string AttentionFormName = "TodoAttentionForm";
    private const string AttentionMoreName = "TodoAttentionMore";

    // Enter opened an attention row, or a redraw destroyed the focused button: focus goes onto the form's first button once it has drawn,
    // but only when focus may move there (nowhere, detached, or already on an attention stop). A sync must not pull it off the quick-add box or another region.
    private void OnAttentionButtons(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (FocusMayMoveToAttention())
            {
                FocusAttentionButton();
            }
        }, DispatcherPriority.Loaded);

    private bool FocusMayMoveToAttention() =>
        FocusManager?.GetFocusedElement() is not Visual focused || ReferenceEquals(focused, this) || TopLevel.GetTopLevel(focused) is null || AttentionStopOf(focused) is not null;

    // A pick took its row away: focus goes to the next attention line, else to the todo row the panel selected, else to the panel.
    private void OnTodoPicked(object? sender, TodoPickedEventArgs e) =>
        Dispatcher.UIThread.Post(() => FocusAttentionLine(fallback: true), DispatcherPriority.Loaded);

    private static bool IsAttentionName(string? name) =>
        string.Equals(name, AttentionLineName, StringComparison.Ordinal) || string.Equals(name, AttentionFormName, StringComparison.Ordinal) || string.Equals(name, AttentionMoreName, StringComparison.Ordinal);

    // The keys of the attention stops. Space and Enter on a button are the button's own.
    private bool AttentionKey(ShellViewModel shell, Visual focused, KeyEventArgs e) =>
        AttentionStopOf(focused) is { } stop && e.KeyModifiers == KeyModifiers.None
        && (PickKey(shell, e.Key) || EnterKey(shell, stop, e.Key) || NoPickKey(shell, stop, e.Key));

    private static bool PickKey(ShellViewModel shell, Key key)
    {
        if (key is not (Key.D1 or Key.NumPad1 or Key.D2 or Key.NumPad2))
        {
            return false;
        }

        TaskFaults.Rethrow(shell.Todo.Pick(key is Key.D1 or Key.NumPad1 ? 1 : 2));
        return true;
    }

    private bool EnterKey(ShellViewModel shell, AttentionStop stop, Key key) =>
        key == Key.Enter && stop != AttentionStop.Button && (stop == AttentionStop.More ? OpenNextAttention(shell) : shell.Todo.OpenAttention());

    private static bool NoPickKey(ShellViewModel shell, AttentionStop stop, Key key) =>
        (key is Key.X or Key.Delete or Key.Back || (key == Key.Space && stop != AttentionStop.Button)) && shell.Todo.NoPick();

    private bool OpenNextAttention(ShellViewModel shell)
    {
        var moved = shell.Todo.OpenNextAttention();
        Dispatcher.UIThread.Post(() => FocusAttentionLine(fallback: false), DispatcherPriority.Loaded);
        return moved;
    }

    // Which attention stop has focus: the folded line or the open form, the "Sort out N more" line, or a button inside the form; null on any other stop.
    private static AttentionStop? AttentionStopOf(Visual focused)
    {
        if (focused is Button && focused.GetVisualAncestors().OfType<Border>().Any(b => string.Equals(b.Name, AttentionFormName, StringComparison.Ordinal)))
        {
            return AttentionStop.Button;
        }

        return focused is Border { Name: { } name } && IsAttentionName(name)
            ? string.Equals(name, AttentionMoreName, StringComparison.Ordinal) ? AttentionStop.More : AttentionStop.Line
            : null;
    }

    // The open form's first button takes focus on the keyboard's ring.
    private void FocusAttentionButton()
    {
        var form = AttentionStops().FirstOrDefault(b => string.Equals(b.Name, AttentionFormName, StringComparison.Ordinal));
        var button = form?.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("attention-button") && b.IsEffectivelyVisible);
        _ = (Usable(button) ?? Usable(form))?.Focus(NavigationMethod.Tab);
    }

    // The shown attention row (its line, or its form while open) takes focus; with none shown (after a pick), the todo row the panel selected does when `fallback` says so.
    private void FocusAttentionLine(bool fallback)
    {
        var row = AttentionStops().FirstOrDefault(b => !string.Equals(b.Name, AttentionMoreName, StringComparison.Ordinal));
        if (Usable(row) is { } target)
        {
            _ = target.Focus(NavigationMethod.Tab);
        }
        else if (fallback && Shell is { } shell)
        {
            FocusTodoRow(shell.Todo.SelectedId);
        }
    }

    private IEnumerable<Border> AttentionStops() =>
        this.Get<Control>("Page").GetVisualDescendants().OfType<Border>().Where(b => b.IsEffectivelyVisible && IsAttentionName(b.Name));

    // Where focus is among the attention stops.
    private enum AttentionStop
    {
        Line,
        More,
        Button,
    }
}
