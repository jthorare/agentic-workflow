namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Settings' Obsidian todo note card (<see cref="TodoNoteCardViewModel"/>, continued): the reads and the buttons, each one or two calls to the API.</summary>
internal sealed partial class TodoNoteCardViewModel
{
    /// <summary>Reads where the sync stands and the library's vaults. A shown check, or a call in flight, is not disturbed.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the card shows what is true.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Check note: reads the note and writes nothing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the result is shown.</returns>
    internal Task Check(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Start syncing, or Start syncing (one way): saves the key.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the settled line is shown.</returns>
    internal Task Start(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Import and start syncing: saves the key, then imports. A note whose open count changed since Check note is asked about again; the key is taken back out.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when the settled line, or the new question, is shown.</returns>
    internal Task Import(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Cancel: drops the shown check (or the Change edit) and saves nothing.</summary>
    internal void Cancel() => throw new NotSupportedException();

    /// <summary>Change: opens the fields on the running sync's vault and note.</summary>
    internal void Change() => throw new NotSupportedException();

    /// <summary>Stop syncing: asks first.</summary>
    internal void AskStop() => throw new NotSupportedException();

    /// <summary>Keep syncing: drops the stop question.</summary>
    internal void KeepSyncing() => throw new NotSupportedException();

    /// <summary>Stop syncing, confirmed: removes the key. The note stays as it is.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the fields are back.</returns>
    internal Task Stop(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>The no-vault sentence's button: asks for the Library card to be brought into view.</summary>
    internal void OpenLibrary() => RaiseLibraryRequested();
}
