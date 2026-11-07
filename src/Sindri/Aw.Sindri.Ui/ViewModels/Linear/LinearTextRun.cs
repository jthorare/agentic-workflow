namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>A stretch of a description or comment line: plain text, or a web link that opens in the browser.</summary>
internal sealed class LinearTextRun
{
    private readonly Func<Uri, CancellationToken, Task<bool>>? _open;

    internal LinearTextRun(string text, Uri? link = null, Func<Uri, CancellationToken, Task<bool>>? open = null)
    {
        (Text, Link, _open) = (text, link, open);
    }

    /// <summary>What is drawn; for a link it names the host.</summary>
    public string Text { get; }

    /// <summary>Where a link goes; null for plain text.</summary>
    public Uri? Link { get; }

    /// <summary>True when the run is a link.</summary>
    public bool IsLink => Link is not null;

    /// <summary>Opens the link in the system's browser; nothing for plain text.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the system has taken it.</returns>
    internal async Task Open(CancellationToken cancellationToken)
    {
        if (Link is { } uri && _open is { } open)
        {
            _ = await open(uri, cancellationToken).ConfigureAwait(true);
        }
    }
}
