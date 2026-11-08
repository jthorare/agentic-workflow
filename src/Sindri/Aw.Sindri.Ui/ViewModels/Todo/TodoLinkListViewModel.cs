using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The small list O opens on a row with more than one link (todo spec §3.3): J and K move, Enter opens, Esc closes.</summary>
internal sealed class TodoLinkListViewModel : ObservableObject
{
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The list's name for a screen reader.</summary>
    public string Name => Labels.TodoLinkListName;

    /// <summary>The line of keys under the list.</summary>
    public string Hint => Labels.TodoLinkListHint;
#pragma warning restore CA1822, MA0041

    /// <summary>True while the list shows.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The links of the todo it was opened on.</summary>
    public IReadOnlyList<TodoLinkItemViewModel> Items { get; private set => Set(ref field, value); } = [];

    /// <summary>The line J, K and Enter act on.</summary>
    public int SelectedIndex { get; private set => Set(ref field, value); }

    /// <summary>The line J, K and Enter act on; null while the list is closed.</summary>
    internal TodoLinkItemViewModel? Selected => IsOpen && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    /// <summary>Shows the list with the first line selected.</summary>
    /// <param name="links">The todo's links.</param>
    internal void Open(IReadOnlyList<TodoLinkView> links)
    {
        Items = [.. links.Select((l, i) => new TodoLinkItemViewModel(l.Ref, l.Href) { IsSelected = i == 0 })];
        SelectedIndex = 0;
        IsOpen = Items.Count > 0;
    }

    /// <summary>Moves the selection; the first and the last line hold.</summary>
    /// <param name="step">1 for J, -1 for K.</param>
    internal void Move(int step)
    {
        if (Selected is not { } from)
        {
            return;
        }

        from.IsSelected = false;
        SelectedIndex = Math.Clamp(SelectedIndex + step, 0, Items.Count - 1);
        Items[SelectedIndex].IsSelected = true;
    }

    /// <summary>Closes the list.</summary>
    /// <returns>True when it was open.</returns>
    internal bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        (IsOpen, Items, SelectedIndex) = (false, [], 0);
        return true;
    }
}
