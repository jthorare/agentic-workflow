namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The Connect GitHub / Sign in again action (#2499). Red stubs: the behaviour is not written yet.</summary>
internal sealed partial class ReviewsPageViewModel
{
#pragma warning disable CA1822, MA0041 // red stub: becomes instance state when written
    /// <summary>True while GitHub has no usable connection: the page is the one Connect GitHub / Sign in again action.</summary>
    public bool IsConnect => false;

    /// <summary>The action's words ("Connect GitHub", "Sign in to GitHub again"); empty when there is no action.</summary>
    public string ConnectText => string.Empty;
#pragma warning restore CA1822, MA0041

    /// <summary>The connect event: asks the shell to open Connections (the connection's sheet, or the add flow for GitHub).</summary>
#pragma warning disable CS0067 // red stub: raised once PressConnect is written
    internal event EventHandler<ReviewOpenRequestedEventArgs>? ConnectRequested;
#pragma warning restore CS0067

    /// <summary>The action was pressed.</summary>
    internal void PressConnect() => throw new NotSupportedException("red stub: #2499");
}
