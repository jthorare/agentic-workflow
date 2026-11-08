using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Todo;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Todo;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// The Todo panel's keys (<see cref="ShellWindow"/>, continued; todo spec §3.3, D6): Tab between Home's two focus regions, J, K, Up and Down between the panel's stops,
/// X, Space, Delete and Backspace on a row, and Esc folding the expanded panel. A key acts only on the region that has focus.
/// </summary>
internal sealed partial class ShellWindow
{
    private void ListenTodo(ShellViewModel shell, bool on)
    {
        if (on)
        {
            shell.Todo.FocusRequested += OnTodoFocus;
            shell.Todo.AttentionButtonsRequested += OnAttentionButtons;
            shell.Todo.Picked += OnTodoPicked;
        }
        else
        {
            shell.Todo.FocusRequested -= OnTodoFocus;
            shell.Todo.AttentionButtonsRequested -= OnAttentionButtons;
            shell.Todo.Picked -= OnTodoPicked;
        }
    }

    // A key changed a todo: focus goes to the row it names (the next one, the one that came back), or to the panel when none is left.
    private void OnTodoFocus(object? sender, TodoFocusEventArgs request) =>
        Dispatcher.UIThread.Post(() => FocusTodoRow(request.Id), DispatcherPriority.Loaded);

    private bool TodoKey(KeyEventArgs e)
    {
        if (Shell is not { IsHomeShown: true, IsTodoShown: true } shell || OverlayOpen || KeysBlocked(shell, e) || FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return false;
        }

        if (e.Key == Key.Tab)
        {
            return TabBetweenRegions(shell, e.KeyModifiers, focused);
        }

        return TodoPanelOf(focused) is { } panel && (AttentionKey(shell, focused, e) || PanelKey(shell, panel, focused, e));
    }

    // Tab or Shift+Tab in the panel goes to the Needs you list (its selected row, else the first); Shift+Tab on the first Needs you row comes back to the panel.
    // Anywhere else Tab walks the controls as before.
    private bool TabBetweenRegions(ShellViewModel shell, KeyModifiers modifiers, Visual focused)
    {
        if (modifiers is not (KeyModifiers.None or KeyModifiers.Shift))
        {
            return false;
        }

        if (TodoPanelOf(focused) is not null)
        {
            if (shell.NeedsYou.Rows.Count == 0)
            {
                return false;
            }

            FocusNeedsYouRow(shell.NeedsYou.SelectedId ?? shell.NeedsYou.Rows[0].Id);
            return true;
        }

        if (modifiers != KeyModifiers.Shift || !IsFirstNeedsYouStop(shell, focused))
        {
            return false;
        }

        FocusTodoRow(shell.Todo.SelectedId);
        return true;
    }

    private static bool IsFirstNeedsYouStop(ShellViewModel shell, Visual focused) =>
        focused is Border frame && string.Equals(frame.Name, "RowFrame", StringComparison.Ordinal) && shell.NeedsYou.Rows.Count > 0
        && focused.GetVisualAncestors().OfType<NeedsYouRow>().FirstOrDefault() is { } row && string.Equals(row.RowId, shell.NeedsYou.Rows[0].Id, StringComparison.Ordinal);

    private static Border? TodoPanelOf(Visual focused) =>
        focused.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "TodoPanelFrame", StringComparison.Ordinal));

    // J, K, Up and Down walk the stops. X, Delete and Backspace are spent in the panel even where they do nothing (the Show all button, the From elsewhere line),
    // so they can't reach Needs you; Space is spent on a row only, so it still presses a button.
    private static bool PanelKey(ShellViewModel shell, Border panel, Visual focused, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return false;
        }

        if (shell.Todo.LinkList.IsOpen && LinkListKey(shell, e.Key))
        {
            return true;
        }

        if (StepOf(e.Key) is { } step)
        {
            return MoveInPanel(panel, focused, step);
        }

        var row = RowOf(focused);
        if (e.Key is Key.Enter or Key.O)
        {
            return RowOpen(shell, row, e.Key);
        }

        if (e.Key is not (Key.X or Key.Delete or Key.Back) && (e.Key != Key.Space || row is null))
        {
            return false;
        }

        return Act(row, e.Key is Key.Delete or Key.Back ? shell.Todo.Remove : shell.Todo.Toggle);
    }

    // Enter edits the row in place, O opens its links; on any other stop of the panel O is spent (Needs you's O must not see it) and Enter keeps its own meaning.
    private static bool RowOpen(ShellViewModel shell, TodoRowViewModel? row, Key key)
    {
        if (row is null)
        {
            return key == Key.O;
        }

        if (key == Key.O)
        {
            shell.Todo.OpenLinks(row.Id);
        }
        else
        {
            _ = shell.Todo.BeginEdit(row.Id);
        }

        return true;
    }

    // While the link list shows, J, K and the arrows move in it and Enter opens the selected link; the row keeps focus.
    private static bool LinkListKey(ShellViewModel shell, Key key)
    {
        if (StepOf(key) is { } step)
        {
            shell.Todo.LinkList.Move(step);
            return true;
        }

        if (key != Key.Enter)
        {
            return false;
        }

        shell.Todo.OpenSelectedLink();
        return true;
    }

    private static int? StepOf(Key key) => key is Key.J or Key.Down ? 1 : key is Key.K or Key.Up ? -1 : null;

    // The todo whose row frame has focus; null on any other stop.
    private static TodoRowViewModel? RowOf(Visual focused) =>
        focused is Border frame && frame.Classes.Contains("todo-row") ? frame.DataContext as TodoRowViewModel : null;

    private static bool Act(TodoRowViewModel? row, Func<string, Task> act)
    {
        if (row is not null)
        {
            TaskFaults.Rethrow(act(row.Id));
        }

        return true;
    }

    // Moves focus to the next or previous stop; the first and the last hold, so the keys never fall out of the panel.
    private static bool MoveInPanel(Visual panel, Visual focused, int step)
    {
        List<InputElement> stops = [.. panel.GetVisualDescendants().OfType<InputElement>().Where(IsPanelStop)];
        var at = stops.FindIndex(s => ReferenceEquals(s, focused) || s.IsVisualAncestorOf(focused));
        var to = Math.Clamp(at < 0 ? 0 : at + step, 0, Math.Max(stops.Count - 1, 0));
        if (stops.Count > 0 && to != at)
        {
            _ = stops[to].Focus(NavigationMethod.Tab);
        }

        return true;
    }

    // The panel's focus stops: the todo rows, Show all, Show done and the From elsewhere line.
    private static bool IsPanelStop(InputElement c) =>
        c is { Focusable: true, IsEffectivelyVisible: true, IsEffectivelyEnabled: true }
        && (c is Border b ? b.Classes.Contains("todo-row") || string.Equals(b.Name, "ElsewhereFrame", StringComparison.Ordinal) || IsAttentionName(b.Name)
            : c is Button && (string.Equals(c.Name, "TodoShowAll", StringComparison.Ordinal) || string.Equals(c.Name, "TodoDoneToggle", StringComparison.Ordinal)));

    // Esc leaves an open attention row's buttons (it folds to its line), then folds the expanded panel (after the chord, the quick-add box and the layers above); focus stays on the same todo, else the panel.
    private bool EscapeTodoPanel(ShellViewModel shell)
    {
        if (!shell.IsHomeShown || shell.IsSheetShown)
        {
            return false;
        }

        if (FocusManager?.GetFocusedElement() is Visual focused && TodoPanelOf(focused) is not null && shell.Todo.CloseAttention())
        {
            Dispatcher.UIThread.Post(() => FocusAttentionLine(fallback: true), DispatcherPriority.Loaded);
            return true;
        }

        if (!shell.Todo.IsExpanded)
        {
            return false;
        }

        shell.Todo.Fold();
        var selected = shell.Todo.SelectedId;
        Dispatcher.UIThread.Post(() => FocusTodoRow(selected), DispatcherPriority.Loaded);
        return true;
    }

    // The row of that todo takes focus on the keyboard's ring; with no such row drawn, the panel does (its first row, else its frame).
    private void FocusTodoRow(string? id)
    {
        var row = id is null ? null : this.Get<Control>("Page").GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("todo-row") && b.IsEffectivelyVisible && b.DataContext is TodoRowViewModel r && string.Equals(r.Id, id, StringComparison.Ordinal));
        if (Usable(row) is { } target)
        {
            _ = target.Focus(NavigationMethod.Tab);
            return;
        }

        FocusTodoPanel();
    }
}
