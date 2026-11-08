using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Todos;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The Todo panel on Home (todo spec §3.1, §5): the owner's open todos grouped by day, collapsed to five rows or expanded to the groups
/// with a Show done toggle. It reads on connect and on every event, and regroups at local midnight.
/// </summary>
internal sealed class TodoPanelViewModel : ObservableObject, IDisposable
{
    private const int CollapsedRows = 5;
    private static readonly TimeSpan _doneWithin = TimeSpan.FromDays(7);
    private static readonly TimeSpan _removedWithin = TimeSpan.FromHours(24);

    private readonly Dictionary<string, TodoRowViewModel> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TodoGroupViewModel> _groups = new(StringComparer.Ordinal);
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private readonly UiContext _ui = new();
    private IReadOnlyList<TodoView>? _todos;
    private ITimer? _midnight;
    private int _reads;

    internal TodoPanelViewModel(ISindriApi api, TimeProvider clock)
    {
        (_api, _clock) = (api, clock);
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

    /// <summary>"Nothing on your list. Press N to add one." while nothing is open; empty otherwise.</summary>
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
    public string DoneToggleText { get; private set => Set(ref field, value); } = Labels.TodoShowDone;

    /// <summary>The toggle's name for a screen reader: "Show done todos, 4".</summary>
    public string DoneToggleName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Nothing done in the last 7 days" while Show done is on and there is nothing to list.</summary>
    public string DoneEmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The id of the todo the focus is on; kept across a re-read and a regroup, dropped once the todo is gone.</summary>
    public string? SelectedId { get; set => Set(ref field, value); }

    /// <summary>Reads the list (on connect and on every event). It asks for every state: the panel works out the Done and Removed windows itself.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var list = await _api.Todos(url, "all", cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since: its answer is the one to show
        }

        IsLoading = false;   // after a failure the last good answer stays under the line
        if (list.IsError)
        {
            Error = Labels.TodoFailed(list.Error);
            return;
        }

        (Error, _todos) = (string.Empty, list.Value.Todos);
        Show();
        ScheduleMidnight();
    }

    /// <summary>Shows all groups ("Show all 11").</summary>
    internal void Expand()
    {
        IsExpanded = true;
        Show();
    }

    /// <summary>Folds back to five rows; the Done group goes away again.</summary>
    internal void Fold()
    {
        (IsExpanded, ShowsDone) = (false, false);
        Show();
    }

    /// <summary>Shows or hides the Done group; the collapsed panel has no toggle.</summary>
    internal void ToggleDone()
    {
        if (!IsExpanded)
        {
            return;
        }

        ShowsDone = !ShowsDone;
        Show();
    }

    /// <summary>Stops the midnight timer.</summary>
    public void Dispose()
    {
        _midnight?.Dispose();
        _midnight = null;
    }

    // One shot at the next local midnight; the callback regroups and sets the next one.
    private void ScheduleMidnight()
    {
        _midnight?.Dispose();
        var local = _clock.GetLocalNow();
        var next = local.Date.AddDays(1);
        var due = new DateTimeOffset(next, _clock.LocalTimeZone.GetUtcOffset(next)) - _clock.GetUtcNow();
        _midnight = _clock.CreateTimer(_ => _ui.PostOrInline(Regroup), state: null, due, Timeout.InfiniteTimeSpan);
    }

    private void Regroup()
    {
        Show();
        ScheduleMidnight();
    }

    private void Show()
    {
        if (_todos is not { } todos)
        {
            return;
        }

        var today = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
        var split = Split.Of(todos, today, _clock.GetUtcNow());
        var showsDone = IsExpanded && ShowsDone;
        var wanted = new HashSet<string>(split.Open.Select(t => t.Id), StringComparer.Ordinal);
        if (showsDone)
        {
            wanted.UnionWith(split.Done.Concat(split.Removed).Select(t => t.Id));
        }

        foreach (var gone in _rows.Keys.Where(k => !wanted.Contains(k)).ToList())
        {
            _ = _rows.Remove(gone);
        }

        Patch.To(Rows, [.. split.Overdue.Concat(split.Due).Concat(split.Upcoming).Concat(split.Undated).Take(CollapsedRows).Select(t => Row(t, today))]);
        var groups = new List<TodoGroupViewModel>();
        AddGroup(groups, Labels.TodoGroupOverdue, split.Overdue, today);
        AddGroup(groups, Labels.TodoGroupToday, split.Due, today);
        AddGroup(groups, Labels.TodoGroupUpcoming, split.Upcoming, today);
        AddGroup(groups, Labels.TodoGroupNoDate, split.Undated, today);
        if (showsDone)
        {
            AddGroup(groups, Labels.TodoGroupDone, split.Done, today);
            AddGroup(groups, Labels.TodoGroupRemoved, split.Removed, today);
        }

        Patch.To(Groups, groups);
        ShowSummary(split, todos);
    }

    private void ShowSummary(Split split, IReadOnlyList<TodoView> todos)
    {
        var open = split.Open.Count;
        var listed = split.Done.Count + split.Removed.Count;
        CountLine = Labels.TodoCountLine(open, split.Overdue.Count, split.Open.Count(t => t.Conflict is not null));
        (IsEmpty, EmptyText) = (open == 0, open == 0 ? Labels.TodoEmpty : string.Empty);
        (ShowsShowAll, ShowAllText) = (!IsExpanded && open > CollapsedRows, Labels.TodoShowAll(open));
        DoneToggleText = ShowsDone ? Labels.TodoHideDone : Labels.TodoShowDone;
        DoneToggleName = Labels.TodoShowDoneName(listed);
        DoneEmptyText = IsExpanded && ShowsDone && listed == 0 ? Labels.TodoNothingDone : string.Empty;
        if (SelectedId is { } selected && !todos.Any(t => string.Equals(t.Id, selected, StringComparison.Ordinal)))
        {
            SelectedId = null;
        }
    }

    // A group is drawn only while it has todos.
    private void AddGroup(List<TodoGroupViewModel> groups, string heading, List<TodoView> todos, DateOnly today)
    {
        if (todos.Count == 0)
        {
            return;
        }

        if (!_groups.TryGetValue(heading, out var group))
        {
            group = _groups[heading] = new TodoGroupViewModel(heading);
        }

        group.Show([.. todos.Select(t => Row(t, today))]);
        groups.Add(group);
    }

    private TodoRowViewModel Row(TodoView todo, DateOnly today)
    {
        if (_rows.TryGetValue(todo.Id, out var kept))
        {
            kept.Update(todo, today);
            return kept;
        }

        return _rows[todo.Id] = new TodoRowViewModel(todo, today);
    }

    // The list cut into the groups for one day: open todos by due day (the server's order breaks a tie, OrderBy is stable), then what was done in a week and removed in a day.
    private sealed record Split(List<TodoView> Open, List<TodoView> Overdue, List<TodoView> Due, List<TodoView> Upcoming, List<TodoView> Undated, List<TodoView> Done, List<TodoView> Removed)
    {
        internal static Split Of(IReadOnlyList<TodoView> todos, DateOnly today, DateTimeOffset now)
        {
            var open = todos.Where(t => !t.Removed && t.DoneAt is null).ToList();
            return new Split(
                open,
                [.. open.Where(t => t.Due < today).OrderBy(t => t.Due)],
                [.. open.Where(t => t.Due == today)],
                [.. open.Where(t => t.Due > today).OrderBy(t => t.Due)],
                [.. open.Where(t => t.Due is null)],
                [.. todos.Where(t => !t.Removed && t.DoneAt >= now - _doneWithin).OrderByDescending(t => t.DoneAt)],
                [.. todos.Where(t => t.Removed && t.RemovedAt >= now - _removedWithin).OrderByDescending(t => t.RemovedAt)]);
        }
    }
}
