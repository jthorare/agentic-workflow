using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Settings' Connections → Obsidian todo note card in the shell (<see cref="ShellViewModel"/>, continued; todo spec §3.4): read when Settings opens and, while it is shown, again on connect and after every event.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Obsidian todo note card behind Settings' Connections group.</summary>
    public TodoNoteCardViewModel TodoNoteCard => field ??= InitTodoNoteCard();

    private TodoNoteCardViewModel InitTodoNoteCard()
    {
        var card = new TodoNoteCardViewModel(Health.ApiUrl, _api, _clock);
        Health.Follow((url, token) => CurrentPage == PageId.Settings ? card.Refresh(url, token) : Task.CompletedTask);
        return card;
    }

    private void ReadTodoNoteCard()
    {
        if (Health.ApiUrl() is { } url)
        {
            _ = TodoNoteCard.Refresh(url, CancellationToken.None);
        }
    }
}
