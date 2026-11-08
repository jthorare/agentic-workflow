using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Settings' Obsidian todo note card (<see cref="TodoNoteCardViewModel"/>, continued): the import step, what follows a refusal, and the profile-moved (409) reads.</summary>
internal sealed partial class TodoNoteCardViewModel
{
    // The import after the saved key; false when it was refused (the key is taken back and the refusal shown).
    private async Task<bool> ImportChecked(Uri url, int expectOpen, bool keyChanged, CancellationToken cancellationToken)
    {
        var (vault, note) = (_checkedVault, _checkedNote);
        StatusText = Labels.TodoCardImporting;
        Changed();
        var imported = await _api.ImportTodoNote(url, vault, note, expectOpen, cancellationToken).ConfigureAwait(true);
        if (!imported.IsError)
        {
            return true;
        }

        await TakeBack(url, keyChanged).ConfigureAwait(true);
        await Refused(url, vault, note, imported.Error, cancellationToken).ConfigureAwait(true);
        return false;
    }

    // The import was refused after the key was saved: put the profile back as it was (the old note while Change edits one, else no key).
    private async Task TakeBack(Uri url, bool changed)
    {
        if (!changed)
        {
            return;
        }

        var back = _changing && _note is { Vault: { } vault, Note: { } note }
            ? await _api.SetTodoNote(url, vault, note, _profileHash, CancellationToken.None).ConfigureAwait(true)
            : await _api.StopTodoNote(url, _profileHash, CancellationToken.None).ConfigureAwait(true);
        _profileHash = back.IsSuccess ? back.Value.ProfileHash : null;
    }

    // SND-TODO-409 (or any -409) on a set or a stop: the live profile is not the one the last answer's hash named.
    private static bool IsProfileMoved(Error error) => error.Code.EndsWith("-409", StringComparison.Ordinal);

    // The set found the profile moved: forget the hash (the next call is unconditional, after the owner saw what is true), read the note and check it again, and say why.
    private async Task SetMoved(Uri url, string vault, string note, CancellationToken cancellationToken)
    {
        _profileHash = null;
        var read = await _api.TodoNote(url, cancellationToken).ConfigureAwait(true);
        _note = read.IsSuccess && read.Value.Note is not null && !string.Equals(read.Value.State, "not set", StringComparison.Ordinal) ? read.Value : null;
        var again = await _api.CheckTodoNote(url, vault, note, cancellationToken).ConfigureAwait(true);
        if (again.IsError)
        {
            (ErrorText, StatusText, _check) = (Labels.TodoCardError(again.Error), string.Empty, null);
            Stage = TodoNoteCardStage.Form;
            return;
        }

        (_check, StatusText) = (again.Value, Labels.TodoCardProfileMoved);
    }

    // The stop found the profile moved: forget the hash, read the note again so the card shows what is true now, and say why.
    private async Task StopMoved(Uri url, CancellationToken cancellationToken)
    {
        _profileHash = null;
        IsConfirmingStop = false;
        var read = await _api.TodoNote(url, cancellationToken).ConfigureAwait(true);
        if (read.IsSuccess)
        {
            ApplyNote(read.Value);
        }

        StatusText = Labels.TodoCardProfileMoved;
    }

    // SND-TODO-409: the note's open count is not the one shown. Read it again, say the new count, and ask again. Any other refusal is shown as it came.
    private async Task Refused(Uri url, string vault, string note, Error error, CancellationToken cancellationToken)
    {
        if (!string.Equals(error.Code, TodoConflict, StringComparison.Ordinal))
        {
            (ErrorText, StatusText) = (Labels.TodoCardError(error), string.Empty);
            return;
        }

        var again = await _api.CheckTodoNote(url, vault, note, cancellationToken).ConfigureAwait(true);
        if (again.IsError)
        {
            (ErrorText, StatusText, _check) = (Labels.TodoCardError(again.Error), string.Empty, null);
            Stage = TodoNoteCardStage.Form;
            return;
        }

        (_check, StatusText) = (again.Value, Labels.TodoCardChanged(note, again.Value.Open));
    }

    // The key is saved: read the note's state for the settled line (its last sync) and show it.
    private async Task Settle(Uri url, CancellationToken cancellationToken)
    {
        var read = await _api.TodoNote(url, cancellationToken).ConfigureAwait(true);
        _note = read.IsSuccess && read.Value.Note is not null ? read.Value : null;
        (_check, StatusText, IsConfirmingStop, _changing) = (null, string.Empty, false, false);
        Stage = TodoNoteCardStage.Syncing;
    }
}
