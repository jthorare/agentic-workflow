using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The Todo panel on Home (todo spec §3.1, §5): the owner's open todos grouped by day, collapsed to five rows or expanded to the groups
/// with a Show done toggle. It reads on connect and on every event, and regroups at local midnight.
/// </summary>
internal sealed class TodoPanelViewModel : ObservableObject
{
    internal TodoPanelViewModel(ISindriApi api, TimeProvider clock)
    {
        _ = (api, clock);
    }

    /// <summary>The panel's name.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Title => Labels.TodoTitle;
#pragma warning restore CA1822, MA0041

    /// <summary>True until the first answer.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); } = true;

    /// <summary>The last read's failure line; empty when the last read worked.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"11 open · 1 overdue", plus "· 1 to sort out" while a conflict is open.</summary>
    public string CountLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when no todo is open.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>"Nothing on your list. Press N to add one."</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the panel shows the groups.</summary>
    public bool IsExpanded { get; private set => Set(ref field, value); }

    /// <summary>True while the Done group is on.</summary>
    public bool ShowsDone { get; private set => Set(ref field, value); }

    /// <summary>The first five open rows across the groups (shown while collapsed).</summary>
    public ObservableCollection<TodoRowViewModel> Rows { get; } = [];

    /// <summary>The groups that have todos, overdue first (shown while expanded), then Done and Removed when Show done is on.</summary>
    public ObservableCollection<TodoGroupViewModel> Groups { get; } = [];

    /// <summary>True when more than five are open and the panel is collapsed.</summary>
    public bool ShowsShowAll { get; private set => Set(ref field, value); }

    /// <summary>"Show all 11".</summary>
    public string ShowAllText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Show done" or "Hide done".</summary>
    public string DoneToggleText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The toggle's name for a screen reader: "Show done todos, 4".</summary>
    public string DoneToggleName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Nothing done in the last 7 days" while Show done is on and there is nothing to list.</summary>
    public string DoneEmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The id of the todo the focus is on; kept across a re-read and a regroup.</summary>
    public string? SelectedId { get; set => Set(ref field, value); }

    /// <summary>Reads the list (on connect and on every event).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _ = (url, cancellationToken);
        Rows.Clear();   // red: reads nothing yet
        return Task.CompletedTask;
    }

    /// <summary>Shows all groups ("Show all 11").</summary>
    internal void Expand() => throw new NotSupportedException();

    /// <summary>Folds back to five rows; the Done group goes away again.</summary>
    internal void Fold() => throw new NotSupportedException();

    /// <summary>Shows or hides the Done group.</summary>
    internal void ToggleDone() => throw new NotSupportedException();
}
