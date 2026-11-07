namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The header's Linear issue chips (<see cref="ReviewPageViewModel"/>, continued): each opens the issue's sheet.</summary>
internal sealed partial class ReviewPageViewModel
{
    /// <summary>A chip asked for its issue's sheet.</summary>
    internal event EventHandler<Linear.LinearIssueRequestedEventArgs>? IssueRequested;

    /// <summary>Asks the shell to open an issue's sheet.</summary>
    /// <param name="key">The issue key.</param>
    internal void OpenIssue(string key) => IssueRequested?.Invoke(this, new Linear.LinearIssueRequestedEventArgs(key));
}
