using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The shell's single keys (<see cref="ShellViewModel"/>, continued; UI spec §8): the G chord (ShellViewModel.Chords.cs),
/// J / K between rows, Enter to open the selected row's sheet, and C to create an item. The window passes a key only when
/// no text field has focus and no layer is open.
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>One single key (a letter, "Enter"); true when it did something.</summary>
    /// <param name="key">The key: "G", "J", "K", "C", "Enter", …</param>
    /// <param name="inSheet">True when focus is in the detail sheet: the row keys (J, K, Enter, Y) belong to the sheet then.</param>
    /// <returns>True when the key was handled.</returns>
    internal bool Press(string key, bool inSheet = false)
    {
        // §8 key guard: for 500 ms after a refresh moved the selection on its own, single keys do nothing.
        if (Queue.IsGuarded)
        {
            return true;
        }

        // The G chord (ShellViewModel.Chords.cs) takes G and its second key; an expired chord is no chord, so its key is a fresh one.
        if (PressKey(key))
        {
            return true;
        }

        return StripKeyPressed(key, inSheet) || NeedsYouKeyPressed(key, inSheet) || RunPageKeyPressed(key, inSheet) || WorkflowsKeyPressed(key, inSheet) || AgentsKeyPressed(key, inSheet) || RecipesKeyPressed(key, inSheet) || Single(key, inSheet) || RowKey(key, inSheet);
    }

    // Y copies the selected row's CLI (§8); / opens the Queue page's filter.
    private bool RowKey(string key, bool inSheet)
    {
        if (key == "Y" && !inSheet && OnRowPage && Queue.HasSelectionOn(RowScope) && Queue.SelectedCopy() is { } control)
        {
            _ = control.Copy(CancellationToken.None);
            return true;
        }

        if (key != "/" || !IsWork)
        {
            return false;
        }

        Queue.OpenFilter();
        return true;
    }

    // C, J / K and Enter (§8), scoped to the pages with rows and, for the row keys, to the page rather than the sheet.
    private bool Single(string key, bool inSheet)
    {
        switch (key)
        {
            case "C":
                Create.Open();
                return true;
            case "J" or "K" when !inSheet && OnRowPage:
                Queue.Move(key == "J" ? 1 : -1, RowScope);
                return true;
            case "Enter" when !inSheet && OnRowPage && Queue.HasSelectionOn(RowScope) && Queue.SelectedId is { } id:
                OpenItem(id);
                return true;
            default:
                return false;
        }
    }

    // The pages with rows, and the rows the keys walk there: Home's panels or the Queue page's groups.
    /// <summary>True when the selected row is drawn on this page and the sheet shows its item.</summary>
    internal bool IsSelectedRowOpen => OnRowPage && Queue.HasSelectionOn(RowScope)
        && string.Equals(Item.ItemId, Queue.SelectedId, StringComparison.Ordinal);

    private bool OnRowPage => IsHome || IsWork;

    private QueueScope RowScope => RowScopeOf(CurrentPage) ?? QueueScope.Page;

    // The rows' scope for a page the shell shows (the queue follows it, silently); null for a page that draws none.
    private static QueueScope? RowScopeOf(PageId page) => page == PageId.Home ? QueueScope.Home : page == PageId.Work ? QueueScope.Page : null;
}
