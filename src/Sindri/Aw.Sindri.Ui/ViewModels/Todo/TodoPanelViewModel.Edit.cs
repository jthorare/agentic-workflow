using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The panel's row edit (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.3, §3.4, D9): Enter edits in place, Enter saves only what changed at the version the edit
/// opened on, Esc cancels. The typed text lives in <see cref="TodoEditViewModel"/>, so a read that lands mid-edit never touches it.
/// </summary>
internal sealed partial class TodoPanelViewModel
{
    private static readonly TimeSpan _savingAfter = TimeSpan.FromMilliseconds(300);
    private ITimer? _savingTimer;
    private bool _editSending;

    /// <summary>The edit that is open, or null.</summary>
    internal TodoEditViewModel? Editing { get; private set; }

    /// <summary>Enter on a row: opens the edit.</summary>
    /// <param name="id">The todo's id.</param>
    /// <returns>True when the edit opened.</returns>
    internal bool BeginEdit(string id)
    {
        if (Editing is not null || Find(id) is not { Removed: false } todo || !_rows.TryGetValue(id, out var row))
        {
            return false;
        }

        var edit = new TodoEditViewModel(id, todo.Version, todo.Text, todo.Due, [.. todo.Links.Select(l => l.Ref)]) { Save = SaveEdit, Cancel = () => CancelEdit() };
        (Editing, row.Edit, SelectedId) = (edit, edit, id);
        return true;
    }

    /// <summary>Esc in a field: closes the edit and sends nothing.</summary>
    /// <returns>True when an edit was open (a save that is already on its way is not stopped).</returns>
    internal bool CancelEdit()
    {
        if (Editing is not { } edit)
        {
            return false;
        }

        if (!_editSending)
        {
            CloseEdit(edit);
        }

        return true;
    }

    /// <summary>Enter in a field: saves what changed.</summary>
    /// <returns>A task that completes when Sindri has answered.</returns>
    internal async Task SaveEdit()
    {
        if (Editing is not { } edit || _editSending || _url is not { } url)
        {
            return;
        }

        (edit.Message, edit.Latest) = (string.Empty, string.Empty);
        var (change, problem) = ChangeOf(edit);
        if (change is null)
        {
            edit.Message = problem;
            return;
        }

        if (change is { Text: null, Due: null, Links: null })
        {
            CloseEdit(edit);
            return;
        }

        _editSending = true;
        _savingTimer = _clock.CreateTimer(_ => _ui.PostOrInline(() => edit.IsSaving = _editSending), state: null, _savingAfter, Timeout.InfiniteTimeSpan);
        var sent = await _api.ChangeTodo(url, edit.Id, change, CancellationToken.None).ConfigureAwait(true);
        _savingTimer?.Dispose();
        (_savingTimer, _editSending, edit.IsSaving) = (null, false, false);
        StaleReads();
        if (sent.IsError)
        {
            await Refused(edit, url, sent.Error).ConfigureAwait(true);
            return;
        }

        Saved(edit, change, sent.Value.Version);
    }

    // What the fields changed, as the write sends it; a null change comes with the line that says why not.
    private (TodoChange? Change, string Problem) ChangeOf(TodoEditViewModel edit)
    {
        var text = edit.Text.Trim();
        if (text.Length == 0)
        {
            return (null, Labels.TodoEditEmpty);
        }

        var word = edit.Due.Trim();
        var day = DayOf(word);
        if (word.Length > 0 && day is null)
        {
            return (null, Labels.TodoEditBadDay);
        }

        string[] refs = [.. edit.Links.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        return (new TodoChange(
            edit.Version,
            Text: string.Equals(text, edit.OriginalText, StringComparison.Ordinal) ? null : text,
            Due: day == edit.OriginalDue ? null : day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            Links: refs.SequenceEqual(edit.OriginalRefs, StringComparer.Ordinal) ? null : refs), string.Empty);
    }

    // The day one word names, read like the quick-add box reads its last word; null for no word or for a word that is no day.
    private DateOnly? DayOf(string word) =>
        word.Length == 0 || word.Any(char.IsWhiteSpace) ? null : TodoQuickParse.Parse("x " + word, DateOnly.FromDateTime(_clock.GetLocalNow().DateTime)).Due;

    // The row shows the new values at the version Sindri answered with; the write's event reads the list again for the links' addresses.
    private void Saved(TodoEditViewModel edit, TodoChange change, long version)
    {
        if (_todos?.FirstOrDefault(t => string.Equals(t.Id, edit.Id, StringComparison.Ordinal)) is { } todo)
        {
            Put(edit.Id, todo with
            {
                Text = change.Text ?? todo.Text,
                Due = change.Due is null ? todo.Due : change.Due.Length == 0 ? null : DateOnly.ParseExact(change.Due, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Links = change.Links is null ? todo.Links : [.. change.Links.Select(r => todo.Links.FirstOrDefault(l => string.Equals(l.Ref, r, StringComparison.Ordinal)) ?? new TodoLinkView(string.Empty, r, string.Empty))],
                Version = version,
            });
        }

        CloseEdit(edit);
    }

    private async Task Refused(TodoEditViewModel edit, Uri url, Error error)
    {
        if (string.Equals(error.Code, "SND-TODO-409", StringComparison.Ordinal))
        {
            await Refresh(url, CancellationToken.None).ConfigureAwait(true);
            if (_todos?.FirstOrDefault(t => string.Equals(t.Id, edit.Id, StringComparison.Ordinal)) is { } latest)
            {
                IReadOnlyList<string> refs = [.. latest.Links.Select(l => l.Ref)];
                edit.Rebase(latest, refs, Labels.TodoEditNow(latest.Text, latest.Due, refs));   // the next Enter saves against the latest, shown beside the fields
                edit.Message = Labels.TodoEditChanged;
            }
        }
        else if (string.Equals(error.Code, "SND-TODO-423", StringComparison.Ordinal))
        {
            await Refresh(url, CancellationToken.None).ConfigureAwait(true);
            CloseEdit(edit);
            if (_rows.TryGetValue(edit.Id, out var row))
            {
                row.Notice = Labels.TodoEditBoth;
            }
        }
        else
        {
            edit.Message = Labels.TodoCouldntSave(Refusal(error));
        }
    }

    private void CloseEdit(TodoEditViewModel edit)
    {
        if (_rows.TryGetValue(edit.Id, out var row))
        {
            row.Edit = null;
        }

        (Editing, SelectedId) = (null, edit.Id);
        OnFocusRequested(edit.Id);
    }
}
