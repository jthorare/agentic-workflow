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
        if (name.Length == 0 || name.Length > MaxName || Definition.Trim().Length > MaxDefinition)
        {
            SubmitError = name.Length == 0 ? Labels.PinSheetNameRequired : Labels.PinSheetTooLong;
            OnChanged(nameof(HasSubmitError));
            return Task.CompletedTask;
        }

        var set = new PinSetBody(
            name,
            Id: _open?.Pin?.Id,
            Summary: Definition.Trim().Length > 0 ? Definition.Trim() : null,
            Aliases: [.. Words.Where(w => w.Ticked).Select(w => w.Word)],
            Features: [.. Features.Where(f => f.Ticked).Select(f => f.Key)]);
        return Write(new PinConceptsBody(Set: [set], IfHash: _ifHash), cancellationToken);
    }

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
        _open?.Pin is { } pin && IsConfirmingUnpin ? Write(new PinConceptsBody(Remove: [pin.Id], IfHash: _ifHash, WithFolderRules: _folderRules > 0), cancellationToken) : Task.CompletedTask;

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
        (SubmitError, IsWriting) = (string.Empty, true);
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
