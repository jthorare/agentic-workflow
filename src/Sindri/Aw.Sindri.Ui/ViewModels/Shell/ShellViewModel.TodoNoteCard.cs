using Aw.Kernel;
using Aw.Sindri.Ui.Api.Reviews;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Settings' Connections → Obsidian todo note card in the shell (<see cref="ShellViewModel"/>, continued; todo spec §3.4): read when Settings opens and, while it is shown, again on connect and after every event.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Obsidian todo note card behind Settings' Connections group.</summary>
    public TodoNoteCardViewModel TodoNoteCard => field ??= InitTodoNoteCard();

    // The Library card's follow refreshes it after every event (RefreshLibraryCards).
    private TodoNoteCardViewModel InitTodoNoteCard() => new(Health.ApiUrl, _api, _clock);

    private void ReadTodoNoteCard(Task<Result<LibraryView>>? library)
    {
        if (Health.ApiUrl() is { } url)
        {
            _ = TodoNoteCard.Refresh(url, library, CancellationToken.None);
        }
    }
}
