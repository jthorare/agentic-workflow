using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>A link fact: its host and title as served, and Open, which hands an http or https link to the platform (no other scheme is opened).</summary>
internal sealed class LinkFactViewer : ObservableObject, IFactViewer
{
    private readonly ILauncher _launcher;

    internal LinkFactViewer(FactRow row, ILauncher launcher)
    {
        Row = row;
        _launcher = launcher;
        var link = row.Ref;
        Url = link?.Url ?? string.Empty;
        CanOpen = Uri.TryCreate(Url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        Host = FactText.Scrub(link?.Host ?? (CanOpen ? new Uri(Url).Host : string.Empty));
        Title = FactText.Scrub(link?.Title ?? row.Summary);
        OpenText = Labels.OpenIn(Host);
        OpenName = Labels.OpenLinkName(Title, Host);
    }

    /// <summary>The fact row the viewer shows.</summary>
    public FactRow Row { get; }

    /// <summary>The link's host as served.</summary>
    public string Host { get; }

    /// <summary>The link's title as served.</summary>
    public string Title { get; }

    /// <summary>The link as served.</summary>
    public string Url { get; }

    /// <summary>True for an http or https link.</summary>
    public bool CanOpen { get; }

    /// <summary>Open's words ("Open in linear.app ↗").</summary>
    public string OpenText { get; }

    /// <summary>Open's accessible name.</summary>
    public string OpenName { get; }

    /// <summary>True when the system refused the link; cleared by the next press that works.</summary>
    public bool LaunchFailed { get; private set; }

    /// <summary>"Couldn't open the link" and the link to copy; empty until a launch fails.</summary>
    public string LaunchFailedText { get; private set; } = string.Empty;

    /// <summary>Hands the link to the system. Only an http or https link is opened.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes once the system answered.</returns>
    public async Task Open(CancellationToken cancellationToken)
    {
        if (CanOpen && Uri.TryCreate(Url, UriKind.Absolute, out var uri))
        {
            var opened = await _launcher.OpenUri(uri, cancellationToken).ConfigureAwait(true);
            (LaunchFailed, LaunchFailedText) = opened ? (false, string.Empty) : (true, Labels.LaunchFailed(uri.AbsoluteUri));
            OnChanged(nameof(LaunchFailed));
            OnChanged(nameof(LaunchFailedText));
        }
    }

    /// <summary>A link viewer needs nothing beyond the row.</summary>
    /// <param name="cancellationToken">Cancels nothing.</param>
    /// <returns>A completed task.</returns>
    public Task Load(CancellationToken cancellationToken) => Task.CompletedTask;
}
