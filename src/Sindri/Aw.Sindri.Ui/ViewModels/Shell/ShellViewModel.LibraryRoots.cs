using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Settings' Library card in the shell (<see cref="ShellViewModel"/>, continued; review page spec §6.3): read when Settings opens and, while it is shown, again on connect and after every event.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Library card behind Settings' Library section.</summary>
    public LibraryRootsViewModel LibraryRoots { get; }

    private LibraryRootsViewModel InitLibraryRoots(ISindriApi api, WindowPorts window)
    {
        var card = new LibraryRootsViewModel(Health.ApiUrl, api, window.Folders, _clock);
        Health.Follow((url, token) => CurrentPage == PageId.Settings ? card.Refresh(url, token) : Task.CompletedTask);
        return card;
    }

    private void ReadLibraryRoots()
    {
        if (Health.ApiUrl() is { } url)
        {
            _ = LibraryRoots.Refresh(url, CancellationToken.None);
        }
    }
}
