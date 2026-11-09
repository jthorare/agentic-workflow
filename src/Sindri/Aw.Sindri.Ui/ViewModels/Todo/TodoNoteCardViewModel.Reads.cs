using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Settings' Obsidian todo note card (<see cref="TodoNoteCardViewModel"/>, continued): the reads of the note and the library, and what the card shows from them.</summary>
internal sealed partial class TodoNoteCardViewModel
{
    // Counts the reads and the clicks that change the card; a read that ends under a newer count is stale and dropped.
    private int _reads;

    // The error text a failed read set, so a later good read clears it and leaves a refusal alone.
    private string _readError = string.Empty;

    /// <summary>Reads where the sync stands and the library's vaults. A shown check, or a call in flight, is not disturbed. A read that throws is shown as an error.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the card shows what is true.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            await Read(url, cancellationToken).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // why: a read runs unobserved from the shell; an exception must show on the card, not end the process
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            if (Stage == TodoNoteCardStage.Reading)
            {
                ErrorText = _readError = Labels.TodoCardUnexpected;
                Changed();
            }
        }
    }

    private async Task Read(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var note = await _api.TodoNote(url, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;
        }

        if (note.IsError)
        {
            NoteReadFailed(note.Error);
            return;   // a Sindri that is down shows its own card elsewhere; the last good card stays
        }

        IsAvailable = true;
        var library = await _api.Library(url, cancellationToken).ConfigureAwait(true);
        if (Held || read != _reads)
        {
            return;   // a click came in while the reads ran: its answer is the one to show
        }

        if (library.IsError)
        {
            LibraryFailed(library.Error, note.Value);
            return;
        }

        if (string.Equals(ErrorText, _readError, StringComparison.Ordinal))
        {
            (ErrorText, _readError) = (string.Empty, string.Empty);   // the failed read is over; a refusal the owner is reading stays
        }

        ApplyVaults(library.Value);
        ApplyNote(note.Value);
        Changed();
    }

    private void NoteReadFailed(Error error)
    {
        if (OlderSindri.IsNotServed(error))
        {
            IsAvailable = false;
            Changed();
        }
        else if (Stage == TodoNoteCardStage.Reading)
        {
            ErrorText = _readError = Labels.TodoCardError(error);   // the first read has no good card to keep
            Changed();
        }
    }

    // The vaults are unknown, so the card can't say there are none; a note that is set doesn't need them.
    private void LibraryFailed(Error error, TodoNoteView note)
    {
        ErrorText = _readError = Labels.TodoCardError(error);
        if (IsSet(note))
        {
            ApplyNote(note);
        }

        Changed();
    }

    private static bool IsSet(TodoNoteView note) => note.Vault is not null && note.Note is not null && !string.Equals(note.State, "not set", StringComparison.Ordinal);

    private void ApplyVaults(LibraryView library)
    {
        Vaults = [.. library.Roots.Where(r => string.Equals(r.Kind, "vault", StringComparison.Ordinal)).Select(r => r.Name)];
        if (!Vaults.Contains(SelectedVault, StringComparer.Ordinal))
        {
            _selectedVault = Vaults.Count > 0 ? Vaults[0] : string.Empty;
        }
    }

    // A note that is set shows the settled line (unless Change is editing it); a note that is not, and a card that showed a stale one, show the fields.
    private void ApplyNote(TodoNoteView set)
    {
        if (set.Vault is not null && set.Note is not null && !string.Equals(set.State, "not set", StringComparison.Ordinal))
        {
            _note = set;
            if (!(_changing && Stage == TodoNoteCardStage.Form))
            {
                (Stage, _checkedVault, _checkedNote) = (TodoNoteCardStage.Syncing, set.Vault, set.Note);
            }
        }
        else if (Stage is TodoNoteCardStage.Reading or TodoNoteCardStage.NoVaults or TodoNoteCardStage.Syncing)
        {
            (_note, IsConfirmingStop) = (null, false);
            Stage = Vaults.Count > 0 ? TodoNoteCardStage.Form : TodoNoteCardStage.NoVaults;
        }
    }
}
