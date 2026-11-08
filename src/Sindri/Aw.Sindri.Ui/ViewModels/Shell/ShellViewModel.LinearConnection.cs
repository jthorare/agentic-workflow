using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Settings' Connections → Linear card in the shell (<see cref="ShellViewModel"/>, continued; Linear hub spec §3): read when Settings opens and, while it is shown, again on connect and after every event.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Linear card behind Settings' Connections group.</summary>
    public LinearConnectionViewModel LinearConnection { get; }

    private LinearConnectionViewModel InitLinearConnection(ISindriApi api)
    {
        var card = new LinearConnectionViewModel(Health.ApiUrl, api);
        Health.Follow((url, token) => CurrentPage == PageId.Settings ? card.Refresh(url, token) : Task.CompletedTask);   // leaving Settings already dropped the click's answer (ShowPage)
        card.ReviewRequested += (_, _) => OpenApproveAll();   // the screen the setup step's Review uses: every waiting profile change together
        return card;
    }

    /// <summary>Connect (the issue sheet's write-access banner, or Work's Connect Linear card): Settings opens with the Linear card in view and its first field focused.</summary>
    internal void OpenLinearConnection()
    {
        var onSettings = CurrentPage == PageId.Settings;
        LinearConnection.Reveal();   // asked before the page opens: its read can finish inside GoTo
        GoTo(PageId.Settings);
        if (onSettings)
        {
            ReadLinearConnection();   // already there, so opening did not read it
        }
    }

    private void ReadLinearConnection()
    {
        if (Health.ApiUrl() is { } url)
        {
            _ = LinearConnection.Refresh(url, CancellationToken.None);
        }
    }
}
