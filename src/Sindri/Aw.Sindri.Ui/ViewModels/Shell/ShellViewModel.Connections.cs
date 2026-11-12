using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Connections page and the connection sheet in the shell (<see cref="ShellViewModel"/>, continued; PR 18a): a tab after Reviews. The page reads while it is the page shown, a row
/// or a 'connection/&lt;name&gt;' link opens the sheet, and the sheet re-reads on every event while it is open.
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Connections page's list and its add form.</summary>
    public ConnectionsPageViewModel Connections { get; private set; }

    /// <summary>The connection sheet.</summary>
    public ConnectionSheetViewModel ConnectionSheet { get; private set; }

    /// <summary>True on Connections.</summary>
    public bool IsConnections => CurrentPage == PageId.Connections;

    /// <summary>True when the sheet's subject is a connection.</summary>
    public bool IsConnectionSheet => SheetLink?.Kind == DeepLinkKind.Connection;

    [MemberNotNull(nameof(Connections), nameof(ConnectionSheet))]
    private void InitConnections(ILauncher launcher)
    {
        Connections = new ConnectionsPageViewModel(_api, launcher, _clock);
        ConnectionSheet = new ConnectionSheetViewModel(_api, launcher, _clock);
        Health.Follow(Connections.Refresh);
        Health.OnTick(Connections.Age);
        Health.Follow((url, token) => IsConnectionSheet && IsSheetOpen ? ConnectionSheet.Refresh(url, token) : Task.CompletedTask);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsConnections), StringComparison.Ordinal))
            {
                Connections.SetShown(IsConnections);
            }
        };
        Connections.OpenRequested += (_, e) => OpenSheet(e.Link);
        ConnectionSheet.Removed += (_, _) =>
        {
            CloseSheet();
            _ = Connections.Retry(CancellationToken.None);   // the list drops the connection at once, not on the next event
        };
        ConnectionSheet.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ConnectionSheetViewModel.Heading), StringComparison.Ordinal))
            {
                Changed(nameof(SheetTitle), nameof(SheetName), nameof(EdgeTabName));
            }
        };
    }

    // The connection part of opening the sheet (OnSheetOpened): the sheet names its connection at once and reads it.
    private void OnConnectionSheetOpened(DeepLink link)
    {
        Changed(nameof(IsConnectionSheet));
        if (link.Kind != DeepLinkKind.Connection || link.Subject is not { } subject)
        {
            return;
        }

        ConnectionSheet.Show(subject);
        if (Health.ApiUrl() is { } url)
        {
            _ = ConnectionSheet.Open(url, subject, CancellationToken.None);
        }
    }

    // The connection part of reopening the sheet (ReopenSheet): the connection may have changed while the sheet was closed, so it is read again before it is trusted.
    private void OnConnectionSheetReopened()
    {
        if (IsConnectionSheet && Health.ApiUrl() is { } url)
        {
            _ = ConnectionSheet.Refresh(url, CancellationToken.None);
        }
    }
}
