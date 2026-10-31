using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The sheet's writes and its Esc (<see cref="PinSheetViewModel"/>, continued): one profile write through the shared flow, the question before Unpin, and the question before a discard.</summary>
internal sealed partial class PinSheetViewModel
{
    /// <summary>Pins (or saves) the sheet's fields in one profile write; the sheet closes when it went through, and stays with every field kept when it didn't.</summary>
    /// <param name="cancellationToken">Cancels the wait for the write's job (the write itself is sent once).</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal Task Submit(CancellationToken cancellationToken)
    {
        var name = Name.Trim();
        if (_ifHash is null)
        {
            return Task.CompletedTask;   // never written without the hash: a missing one reads as no check
        }

        if (name.Length == 0 || name.Length > MaxName || Definition.Trim().Length > MaxDefinition)
        {
            SubmitError = name.Length == 0 ? Labels.PinSheetNameRequired : Labels.PinSheetTooLong;
            OnChanged(nameof(HasSubmitError));
            return Task.CompletedTask;
        }

        var set = new PinSetBody(
            name,
            Id: _open?.Pin?.Id,
            Summary: SummaryToSend(),
            Aliases: [.. Words.Where(w => w.Ticked).Select(w => w.Word)],
            Features: [.. Features.Where(f => f.Ticked).Select(f => f.Key)]);
        return Write(new PinConceptsBody(Set: [set], IfHash: _ifHash), cancellationToken);
    }

    // The definition line; an edited pin's emptied box sends "" (it clears the stored one), any other empty box sends none.
    private string? SummaryToSend() => Definition.Trim() is { Length: > 0 } text ? text : _open?.Pin is not null ? string.Empty : null;

    /// <summary>Asks "Unpin Billing?"; focus goes to Keep pin.</summary>
    internal void AskUnpin()
    {
        if (!CanUnpin || IsWriting || _open?.Pin is not { } pin)
        {
            return;
        }

        (UnpinQuestion, SubmitError) = (Labels.PinSheetUnpinQuestion(pin.Name, _neverApproved, _folderRules), string.Empty);
        IsConfirmingUnpin = true;
        OnChanged(nameof(HasSubmitError));
        KeepPinFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Keeps the pin: the question closes and focus goes back to Unpin.</summary>
    internal void KeepPin()
    {
        if (IsWriting)
        {
            return;
        }

        IsConfirmingUnpin = false;
        UnpinFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes the pin (the same write as a pin, with a removal).</summary>
    /// <param name="cancellationToken">Cancels the wait for the write's job.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal Task Unpin(CancellationToken cancellationToken) =>
        _open?.Pin is { } pin && IsConfirmingUnpin && _ifHash is not null ? Write(new PinConceptsBody(Remove: [pin.Id], IfHash: _ifHash, WithFolderRules: _folderRules > 0), cancellationToken) : Task.CompletedTask;

    /// <summary>Esc: answers the question that shows, else asks before throwing changes away, else closes without writing.</summary>
    /// <returns>True: Esc was spent here (the sheet closes itself through <see cref="Closed"/>).</returns>
    internal bool Escape()
    {
        if (IsWriting)
        {
            return true;
        }

        if (IsConfirmingUnpin)
        {
            KeepPin();
            return true;
        }

        if (IsConfirmingDiscard)
        {
            KeepEditing();
            return true;
        }

        if (IsDirty)
        {
            IsConfirmingDiscard = true;
            KeepEditingFocusRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        Close(written: false);
        return true;
    }

    /// <summary>Discards the changes and closes.</summary>
    internal void Discard() => Close(written: false);

    /// <summary>Goes back to the fields from the discard question.</summary>
    internal void KeepEditing()
    {
        IsConfirmingDiscard = false;
        NameFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task Write(PinConceptsBody body, CancellationToken cancellationToken)
    {
        if (IsWriting || _ports is null)
        {
            return;
        }

        List<string> removed = [.. body.Remove ?? []];
        (SubmitError, ChangedNote, IsWriting) = (string.Empty, string.Empty, true);
        OnChanged(nameof(HasChangedNote));
        Wrote();
        ProfileWritten written;
        try
        {
            written = await Ports.Flow.Run((url, token) => Ports.Api.PinConcepts(url, body, token), path => Mine(path) || removed.Exists(id => path.EndsWith(".concepts." + id, StringComparison.Ordinal)), _approve, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;   // the sheet closed under it; the write itself may still land
        }
        finally
        {
            IsWriting = false;
        }

        Wrote();
        if (written.State is ProfileWriteState.Approved or ProfileWriteState.Waiting)
        {
            Close(written: true);
            return;
        }

        SubmitError = ErrorOf(written);
        OnChanged(nameof(HasSubmitError));
        if (written.State == ProfileWriteState.Clash)
        {
            await Reread(cancellationToken).ConfigureAwait(true);
        }
    }

    // The profile moved: the pins are read again so the next press carries the fresh hash; when that read fails nothing can be written until the sheet is opened again.
    private async Task Reread(CancellationToken cancellationToken)
    {
        var read = Ports.ApiUrl() is { } url ? await Ports.Api.ContextPins(url, _repo, cancellationToken).ConfigureAwait(true) : null;
        if (read is { IsSuccess: true })
        {
            (_ifHash, _pins) = (read.Value.FileHash, read.Value.Pins);
            NoteChange();
            Rejudge();
            await ReadRule(cancellationToken).ConfigureAwait(true);
        }
        else
        {
            (_ifHash, SubmitError) = (null, Labels.PinSheetNoHash);
            OnChanged(nameof(HasSubmitError));
        }

        OnChanged(nameof(CanSubmit));
    }

    // Edit pin: when the re-read copy of this pin differs from the one the sheet opened with, the next press would overwrite that change, so say so.
    private void NoteChange()
    {
        var note = string.Empty;
        if (_open?.Pin is { } opened
            && _pins.FirstOrDefault(p => string.Equals(p.Id, opened.Id, StringComparison.Ordinal)) is { } now
            && (!now.Aliases.SequenceEqual(opened.Aliases, StringComparer.Ordinal)
                || !now.Features.SequenceEqual(opened.Features, StringComparer.Ordinal)
                || !string.Equals(now.Summary ?? string.Empty, opened.Summary ?? string.Empty, StringComparison.Ordinal)))
        {
            note = Labels.PinSheetChangedInFile(opened.Name);
        }

        ChangedNote = note;
        OnChanged(nameof(HasChangedNote));
    }

    // What shows under the button: the moved profile's own line, the server's sentence, the timeout's, or a plain failure.
    private string ErrorOf(ProfileWritten written)
    {
        if (written.State == ProfileWriteState.Clash)
        {
            return Labels.PinSheetClash(Labels.PinSheetSubmit(IsEdit, _approve));
        }

        return string.Equals(written.Code, ProfileWriteFlow.TimedOut, StringComparison.Ordinal) ? Labels.PinSheetTimedOut : written.Message ?? Labels.PinSheetFailed;
    }

    // The button's word and whether it can be pressed follow the write.
    private void Wrote()
    {
        OnChanged(nameof(IsWriting));
        OnChanged(nameof(SubmitText));
        OnChanged(nameof(UnpinConfirmText));
        OnChanged(nameof(CanSubmit));
    }

    private void Close(bool written) => Closed?.Invoke(this, new PinSheetClosedEventArgs(written));
}
