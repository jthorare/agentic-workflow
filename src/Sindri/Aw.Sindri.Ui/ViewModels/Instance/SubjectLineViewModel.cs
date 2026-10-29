using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The frame's subject line: the served label, and Open in when the subject has a web link. The app opens only an
/// http or https link, with no control character in it; a link of any other kind is not offered.
/// </summary>
internal sealed class SubjectLineViewModel : ObservableObject
{
    private readonly string _host = string.Empty;
    private readonly ILauncher _launcher;

    internal SubjectLineViewModel(SubjectRef subject, string address, ILauncher launcher)
    {
        _launcher = launcher;
        Label = subject.Label.Length > 0 ? subject.Label : subject.Key;
        Kind = subject.Kind;
        if (subject.Href is { Length: > 0 } href && Controls.ControlViewModel.Safe(href)
            && Uri.TryCreate(href, UriKind.Absolute, out var uri) && (string.Equals(uri.Scheme, "http", StringComparison.Ordinal) || string.Equals(uri.Scheme, "https", StringComparison.Ordinal)))
        {
            (Href, _host) = (href, uri.Host);
        }

        OpenInName = HasHref ? Labels.OpenInName(_host, address) : string.Empty;
    }

    /// <summary>The served label (its key when it has none).</summary>
    public string Label { get; }

    /// <summary>The subject's kind as served.</summary>
    public string Kind { get; }

    /// <summary>The web link, or empty.</summary>
    public string Href { get; } = string.Empty;

    /// <summary>True when the subject has a link the app may open.</summary>
    public bool HasHref => Href.Length > 0;

    /// <summary>The link's words ("Open in linear.app ↗"); empty without a link.</summary>
    public string OpenInText => HasHref ? Labels.OpenIn(_host) : string.Empty;

    /// <summary>The link's accessible name; it carries the instance.</summary>
    public string OpenInName { get; }

    /// <summary>True when the system refused the link; cleared by the next press that works.</summary>
    public bool LaunchFailed { get; private set; }

    /// <summary>"Couldn't open the link" and the link to copy; empty until a launch fails.</summary>
    public string LaunchFailedText { get; private set; } = string.Empty;

    /// <summary>Hands the subject's link to the system.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes once the system answered.</returns>
    public async Task OpenIn(CancellationToken cancellationToken)
    {
        if (HasHref && Uri.TryCreate(Href, UriKind.Absolute, out var uri))
        {
            var opened = await _launcher.OpenUri(uri, cancellationToken).ConfigureAwait(true);
            (LaunchFailed, LaunchFailedText) = opened ? (false, string.Empty) : (true, Labels.LaunchFailed(uri.AbsoluteUri));
            OnChanged(nameof(LaunchFailed));
            OnChanged(nameof(LaunchFailedText));
        }
    }

    /// <summary>What the line is made of, to keep the control when a re-read serves the same.</summary>
    internal string Signature => string.Join('|', Label, Kind, Href);
}
