using Aw.Sindri.Ui.Api.Reviews;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The Connect GitHub / Sign in again action (#2499): the list's login comes from the GitHub connection, so a missing or expired one is fixed on the Connections page.</summary>
internal sealed partial class ReviewsPageViewModel
{
    private ReviewConnectView? _connect;

    /// <summary>True while GitHub has no usable connection: the page is the one Connect GitHub / Sign in again action.</summary>
    public bool IsConnect => _connect is not null;

    /// <summary>The action's words ("Connect GitHub", "Sign in to GitHub again"); empty when there is no action.</summary>
    public string ConnectText => _connect?.Text ?? string.Empty;

    /// <summary>The action named a connection that needs signing in again: the link is 'connection/&lt;name&gt;', its sheet.</summary>
    internal event EventHandler<ReviewOpenRequestedEventArgs>? ConnectRequested;

    /// <summary>The action has no connection to open (GitHub isn't connected yet): the shell shows the Connections page.</summary>
    internal event EventHandler? ConnectionsRequested;

    /// <summary>The action was pressed.</summary>
    internal void PressConnect()
    {
        if (_connect is not { } connect)
        {
            return;
        }

        if (connect.Connection is { } name && DeepLink.TryParse("connection/" + name, out var link))
        {
            ConnectRequested?.Invoke(this, new ReviewOpenRequestedEventArgs(link));
        }
        else
        {
            ConnectionsRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ShowConnect(ReviewConnectView? connect)
    {
        _connect = connect;
        OnChanged(nameof(IsConnect));
        OnChanged(nameof(ConnectText));
    }
}
