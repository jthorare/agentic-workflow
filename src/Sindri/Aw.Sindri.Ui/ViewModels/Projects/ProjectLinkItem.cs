namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One link on a project's sheet: clickable when it may be, else plain text.</summary>
internal sealed class ProjectLinkItem
{
    private readonly Func<Uri, CancellationToken, Task<bool>> _open;

    internal ProjectLinkItem(string text, string url, bool canOpen, Func<Uri, CancellationToken, Task<bool>> open)
    {
        (Text, Url, CanOpen, _open) = (text, url, canOpen, open);
    }

    /// <summary>The words: "Open in Linear ↗", "web-app PRs ↗", or the owner's label.</summary>
    public string Text { get; }

    /// <summary>Where it goes, as served.</summary>
    public string Url { get; }

    /// <summary>True when the link is https (and, for Linear and GitHub, on that host); a link that is not is shown as text.</summary>
    public bool CanOpen { get; }

    /// <summary>Opens the link in the system's browser; nothing when it is not clickable.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the system has taken it.</returns>
    internal async Task Open(CancellationToken cancellationToken)
    {
        if (CanOpen && Uri.TryCreate(Url, UriKind.Absolute, out var uri))
        {
            _ = await _open(uri, cancellationToken).ConfigureAwait(true);
        }
    }
}
