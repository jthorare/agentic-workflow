using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Settings' Obsidian todo note card (<see cref="TodoNoteCardViewModel"/>, continued): the reads and the buttons, each one or two calls to the API.</summary>
internal sealed partial class TodoNoteCardViewModel
{
    private const string TodoConflict = "SND-TODO-409";

    // The live profile's hash as the last set or stop answered it; sent back as ifHash so a profile edited somewhere else is refused (409) instead of overwritten. Null until a call answered one.
    private string? _profileHash;

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
            Changed();
        }

        StatusText = string.Empty;
        if (!string.Equals(_selectedVault, vault, StringComparison.Ordinal) || !string.Equals(_noteText.Trim(), note, StringComparison.Ordinal))
        {
            Changed();   // the owner edited the fields during the call: the answer is about a note that is no longer typed
            return;
        }

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

        _reads++;
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
            Changed();
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

    /// <summary>A click handler threw: shows a plain error and puts the buttons back.</summary>
    internal void Crashed()
    {
        (IsBusy, StatusText, ErrorText) = (false, string.Empty, Labels.TodoCardUnexpected);
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
        _reads++;
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
