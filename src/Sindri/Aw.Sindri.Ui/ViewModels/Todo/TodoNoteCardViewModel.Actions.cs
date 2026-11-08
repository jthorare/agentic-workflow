using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Settings' Obsidian todo note card (<see cref="TodoNoteCardViewModel"/>, continued): the reads and the buttons, each one or two calls to the API.</summary>
internal sealed partial class TodoNoteCardViewModel
{
    private const string TodoConflict = "SND-TODO-409";

    // The live profile's hash as the last set or stop answered it; sent back as ifHash so a profile edited somewhere else is refused (409) instead of overwritten. Null until a call answered one.
    private string? _profileHash;

    /// <summary>Reads where the sync stands and the library's vaults. A shown check, or a call in flight, is not disturbed.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the card shows what is true.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        var note = await _api.TodoNote(url, cancellationToken).ConfigureAwait(true);
        if (note.IsError)
        {
            if (OlderSindri.IsNotServed(note.Error))
            {
                IsAvailable = false;
                Changed();
            }

            return;   // a Sindri that is down shows its own card elsewhere; the last good card stays
        }

        IsAvailable = true;
        var library = await _api.Library(url, cancellationToken).ConfigureAwait(true);
        if (Held)
        {
            return;   // a click came in while the reads ran: its answer is the one to show
        }

        ApplyVaults(library);
        ApplyNote(note.Value);
        Changed();
    }

    private void ApplyVaults(Result<Aw.Sindri.Ui.Api.Reviews.LibraryView> library)
    {
        if (library.IsSuccess)
        {
            Vaults = [.. library.Value.Roots.Where(r => string.Equals(r.Kind, "vault", StringComparison.Ordinal)).Select(r => r.Name)];
        }
        else
        {
            ErrorText = Labels.TodoCardError(library.Error);
        }

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

    /// <summary>Check note: reads the note and writes nothing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the result is shown.</returns>
    internal async Task Check(CancellationToken cancellationToken)
    {
        if (!CanCheck || _url() is not { } url)
        {
            return;
        }

        var (vault, note) = (_selectedVault, _noteText.Trim());
        (IsBusy, ErrorText, StatusText) = (true, string.Empty, Labels.TodoCardChecking);
        Changed();
        Result<TodoNoteCheckView> read;
        try
        {
            read = await _api.CheckTodoNote(url, vault, note, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }

        StatusText = string.Empty;
        if (read.IsError)
        {
            (_check, ErrorText) = (null, Labels.TodoCardError(read.Error));
            Stage = TodoNoteCardStage.Form;
        }
        else
        {
            (_check, _checkedVault, _checkedNote, Stage) = (read.Value, vault, note, TodoNoteCardStage.Checked);
        }

        Changed();
    }

    /// <summary>Start syncing, or Start syncing (one way): saves the key.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the settled line is shown.</returns>
    internal Task Start(CancellationToken cancellationToken) => ShowStart || ShowStartOneWay ? Save(import: false, cancellationToken) : Task.CompletedTask;

    /// <summary>Import and start syncing: saves the key, then imports. A note whose open count changed since Check note is asked about again; the key is taken back out.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when the settled line, or the new question, is shown.</returns>
    internal Task Import(CancellationToken cancellationToken) => ShowImport ? Save(import: true, cancellationToken) : Task.CompletedTask;

    /// <summary>Cancel: drops the shown check (or the Change edit) and saves nothing.</summary>
    internal void Cancel()
    {
        if (IsBusy || !ShowCancel)
        {
            return;
        }

        (_check, StatusText, ErrorText) = (null, string.Empty, string.Empty);
        Stage = _changing ? TodoNoteCardStage.Syncing : TodoNoteCardStage.Form;
        _changing = false;
        Changed();
    }

    /// <summary>Change: opens the fields on the running sync's vault and note.</summary>
    internal void Change()
    {
        if (Stage != TodoNoteCardStage.Syncing || IsBusy)
        {
            return;
        }

        (_selectedVault, _noteText) = (_note?.Vault ?? _checkedVault, _note?.Note ?? _checkedNote);
        (_changing, IsConfirmingStop, _check, StatusText, ErrorText) = (true, false, null, string.Empty, string.Empty);
        Stage = TodoNoteCardStage.Form;
        Changed();
    }

    /// <summary>Stop syncing: asks first.</summary>
    internal void AskStop()
    {
        if (ShowSyncing && !IsBusy)
        {
            IsConfirmingStop = true;
            Changed();
        }
    }

    /// <summary>Keep syncing: drops the stop question.</summary>
    internal void KeepSyncing()
    {
        IsConfirmingStop = false;
        Changed();
    }

    /// <summary>Stop syncing, confirmed: removes the key. The note stays as it is.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the fields are back.</returns>
    internal async Task Stop(CancellationToken cancellationToken)
    {
        if (!ShowStopAsk || _url() is not { } url)
        {
            return;
        }

        (IsBusy, ErrorText, StatusText) = (true, string.Empty, Labels.TodoCardStopping);
        Changed();
        Result<TodoNoteChangedView> stopped;
        try
        {
            stopped = await _api.StopTodoNote(url, _profileHash, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }

        StatusText = string.Empty;
        if (stopped.IsError && IsProfileMoved(stopped.Error))
        {
            await StopMoved(url, cancellationToken).ConfigureAwait(true);
        }
        else if (stopped.IsError)
        {
            ErrorText = Labels.TodoCardError(stopped.Error);
        }
        else
        {
            _profileHash = stopped.Value.ProfileHash;
            (_note, _check, IsConfirmingStop, _changing) = (null, null, false, false);
            Stage = Vaults.Count > 0 ? TodoNoteCardStage.Form : TodoNoteCardStage.NoVaults;
        }

        Changed();
    }

    /// <summary>The no-vault sentence's button: asks for the Library card to be brought into view.</summary>
    internal void OpenLibrary() => RaiseLibraryRequested();

    // A call is running, or a check is shown: a read of the note must not move the card under the click.
    private bool Held => IsBusy || Stage == TodoNoteCardStage.Checked;

    // The key is saved first (live at once, no approval); an import then needs it, so a refused import takes it back out.
    private async Task Save(bool import, CancellationToken cancellationToken)
    {
        if (_check is not { } check || _url() is not { } url || IsBusy)
        {
            return;
        }

        var (vault, note) = (_checkedVault, _checkedNote);
        (IsBusy, ErrorText, StatusText) = (true, string.Empty, Labels.TodoCardStarting);
        Changed();
        try
        {
            var set = await _api.SetTodoNote(url, vault, note, _profileHash, cancellationToken).ConfigureAwait(true);
            if (set.IsError)
            {
                if (IsProfileMoved(set.Error))
                {
                    await SetMoved(url, vault, note, cancellationToken).ConfigureAwait(true);
                    return;
                }

                ErrorText = Labels.TodoCardError(set.Error);
                StatusText = string.Empty;
                return;
            }

            _profileHash = set.Value.ProfileHash;

            if (import && !await ImportChecked(url, check.Open, set.Value.Changed, cancellationToken).ConfigureAwait(true))
            {
                return;
            }

            await Settle(url, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            Changed();
        }
    }
}
