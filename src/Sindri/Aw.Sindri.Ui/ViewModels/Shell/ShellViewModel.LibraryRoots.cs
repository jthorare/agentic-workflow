using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Settings' Library card in the shell (<see cref="ShellViewModel"/>, continued; review page spec §6.3): read when Settings opens and, while it is shown, again on connect and after every event, one library read shared with the todo note card.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Library card behind Settings' Library section.</summary>
    public LibraryRootsViewModel LibraryRoots { get; }

    private LibraryRootsViewModel InitLibraryRoots(ISindriApi api, WindowPorts window)
    {
        var card = new LibraryRootsViewModel(Health.ApiUrl, api, window.Folders, _clock);
        Health.Follow((url, token) => CurrentPage == PageId.Settings ? RefreshLibraryCards(url, token) : Task.CompletedTask);
        card.ErrorsRequested += (_, _) =>
        {
            _ = Reviews.OpenLibrary(CancellationToken.None);   // the sheet the Reviews header opens
            OpenerLibrary = false;   // Esc and Close have no Reviews button to return to from Settings
        };
        return card;
    }

    // Settings' two cards that show the library (this one and the todo note card) share one GET /sindri/library per event. They run
    // side by side (#3887): the note card reads GET /sindri/todo-note first and awaits the shared library answer only after it.
    private Task RefreshLibraryCards(Uri url, CancellationToken cancellationToken)
    {
        var library = _api.Library(url, cancellationToken);
        return Task.WhenAll(LibraryRoots.Refresh(library), TodoNoteCard.Refresh(url, library, cancellationToken));
    }

    // Opening Settings reads the library once; the todo note card shows the same read (ReadTodoNoteCard).
    private Task<Result<LibraryView>> ReadLibraryRoots(Uri url)
    {
        var library = _api.Library(url, CancellationToken.None);
        _ = LibraryRoots.Refresh(library);
        return library;
    }
}
