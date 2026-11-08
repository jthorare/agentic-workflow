using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The panel's row keys (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.3, §3.4): X or Space checks an open todo off, reopens a done one or restores a removed
/// one, and Delete removes. The row changes at once; a refusal brings it back with the reason, and the toast's Undo sends the opposite write.
/// </summary>
internal sealed partial class TodoPanelViewModel
{
    private readonly Dictionary<string, TodoView> _inFlight = new(StringComparer.Ordinal);
    private Uri? _url;

    /// <summary>True while focus is inside the panel (the Todo region, spec D6); the view sets it.</summary>
    public bool HasFocus { get; internal set => Set(ref field, value); }

    /// <summary>Raised after a key changed a todo and Sindri accepted it: the toast with its Undo.</summary>
    internal event EventHandler<TodoActedEventArgs>? Acted;

    /// <summary>Raised when focus should move to a row (or to the panel when the id is null).</summary>
    internal event EventHandler<TodoFocusEventArgs>? FocusRequested;

    /// <summary>X or Space on a row: checks an open todo off, reopens a done one, restores a removed one.</summary>
    /// <param name="id">The todo's id.</param>
    /// <returns>A task that completes when Sindri has answered.</returns>
    internal Task Toggle(string id) => Find(id) is { } todo
        ? Act(todo, todo.Removed ? TodoMove.Restore : todo.DoneAt is null ? TodoMove.Done : TodoMove.Reopen)
        : Task.CompletedTask;

    /// <summary>Delete or Backspace on a row: removes the todo (it stays restorable).</summary>
    /// <param name="id">The todo's id.</param>
    /// <returns>A task that completes when Sindri has answered.</returns>
    internal Task Remove(string id) => Find(id) is { Removed: false } todo ? Act(todo, TodoMove.Remove) : Task.CompletedTask;

    // The todo as the panel shows it; a row with an open conflict takes no key here (its picks are another slice).
    private TodoView? Find(string id) => _todos?.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal)) is { Conflict: null } todo && !_inFlight.ContainsKey(id) ? todo : null;

    private async Task Act(TodoView before, TodoMove move)
    {
        if (_url is not { } url)
        {
            return;
        }

        var next = NeighbourOf(before.Id);
        var applied = Applied(move, before);
        _inFlight[before.Id] = applied;
        Put(before.Id, applied);
        SelectedId = next;
        OnFocusRequested(next);
        var sent = await Send(move, url, before.Id, before.Version).ConfigureAwait(true);
        _ = _inFlight.Remove(before.Id);
        if (sent.IsError)
        {
            Put(before.Id, before);
            Fail(before.Id, sent.Error);
            return;
        }

        var version = sent.Value.Version;
        Put(before.Id, applied with { Version = version });
        Acted?.Invoke(this, new TodoActedEventArgs(ToastFor(move), () => Undo(url, before, move, version)));
    }

    // Undo: the opposite write at the version Sindri answered with; null when it worked, else why not.
    private async Task<string?> Undo(Uri url, TodoView before, TodoMove move, long version)
    {
        var back = Opposite(move);
        var sent = await Send(back, url, before.Id, version).ConfigureAwait(true);
        if (sent.IsError)
        {
            if (string.Equals(sent.Error.Code, "SND-TODO-409", StringComparison.Ordinal))
            {
                await Refresh(url, CancellationToken.None).ConfigureAwait(true);
                return Labels.TodoUndoChanged;
            }

            return Refusal(sent.Error);
        }

        Put(before.Id, Applied(back, Applied(move, before)) with { Version = sent.Value.Version });
        SelectedId = before.Id;
        OnFocusRequested(before.Id);
        return null;
    }

    // The row leaves (or returns) on the spot: the shown list takes the changed todo.
    private void Put(string id, TodoView now)
    {
        if (_todos is not { } todos)
        {
            return;
        }

        _todos = [.. todos.Select(t => string.Equals(t.Id, id, StringComparison.Ordinal) ? now : t)];
        Show();
    }

    // A read that lands while a write is in flight keeps the todo as the key left it.
    private IReadOnlyList<TodoView> Overlaid(IReadOnlyList<TodoView> read) =>
        _inFlight.Count == 0 ? read : [.. read.Select(t => _inFlight.TryGetValue(t.Id, out var kept) ? kept : t)];

    private void Fail(string id, Error error)
    {
        SelectedId = id;
        if (_rows.TryGetValue(id, out var row))
        {
            row.Notice = Labels.TodoCouldntSave(Refusal(error));
        }

        OnFocusRequested(id);
    }

    private static string Refusal(Error error) => error.Code.StartsWith("SND-UI-", StringComparison.Ordinal) ? Labels.QuickAddNotAnswering : error.Message;

    // The row focus goes to when this one leaves: the next row, else the previous one, else the panel (null).
    private string? NeighbourOf(string id)
    {
        List<string> shown = [.. (IsExpanded ? Groups.SelectMany(g => g.Rows) : Rows).Select(r => r.Id)];
        var at = shown.IndexOf(id);
        return at < 0 ? null : at + 1 < shown.Count ? shown[at + 1] : at > 0 ? shown[at - 1] : null;
    }

    private Task<Result<TodoAcceptedView>> Send(TodoMove move, Uri url, string id, long version) => move switch
    {
        TodoMove.Done => _api.DoneTodo(url, id, version, CancellationToken.None),
        TodoMove.Reopen => _api.ReopenTodo(url, id, version, CancellationToken.None),
        TodoMove.Remove => _api.RemoveTodo(url, id, version, CancellationToken.None),
        TodoMove.Restore => _api.RestoreTodo(url, id, version, CancellationToken.None),
        _ => throw new InvalidOperationException(move.ToString()),
    };

    private TodoView Applied(TodoMove move, TodoView todo) => move switch
    {
        TodoMove.Done => todo with { DoneAt = _clock.GetUtcNow() },
        TodoMove.Reopen => todo with { DoneAt = null },
        TodoMove.Remove => todo with { Removed = true, RemovedAt = _clock.GetUtcNow() },
        TodoMove.Restore => todo with { Removed = false, RemovedAt = null },
        _ => throw new InvalidOperationException(move.ToString()),
    };

    private static string ToastFor(TodoMove move) => move switch
    {
        TodoMove.Done => Labels.TodoToastDone,
        TodoMove.Reopen => Labels.TodoToastReopened,
        TodoMove.Remove => Labels.TodoToastRemoved,
        TodoMove.Restore => Labels.TodoToastRestored,
        _ => throw new InvalidOperationException(move.ToString()),
    };

    private static TodoMove Opposite(TodoMove move) => move switch
    {
        TodoMove.Done => TodoMove.Reopen,
        TodoMove.Reopen => TodoMove.Done,
        TodoMove.Remove => TodoMove.Restore,
        TodoMove.Restore => TodoMove.Remove,
        _ => throw new InvalidOperationException(move.ToString()),
    };

    private void OnFocusRequested(string? id) => FocusRequested?.Invoke(this, new TodoFocusEventArgs(id));

    // The four writes a key can send.
    private enum TodoMove
    {
        Done,
        Reopen,
        Remove,
        Restore,
    }
}
