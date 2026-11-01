using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The whole map's side panel for the node a click pinned: its kind and title, where it came from (with a link when one is served), a short
/// description, and its links grouped by band. Source and description are optional; when the control plane does not serve them nothing shows.
/// </summary>
internal sealed partial class NodePanelViewModel : ObservableObject
{
    private readonly Func<string, (string Label, string Kind)?> _lookup;
    private readonly Action<string> _jump;
    private int _version;
    private Uri? _link;
    private readonly LinkLaunch _launch;

    internal NodePanelViewModel(Func<string, (string Label, string Kind)?> lookup, Action<string> jump)
    {
        (_lookup, _jump) = (lookup, jump);
        _launch = new LinkLaunch(() => Launcher);
        _launch.Forward(OnChanged);
    }

    /// <summary>Reads a node; null while the page has no connection. Set by the page.</summary>
    internal Func<string, CancellationToken, Task<Result<NodeView>?>>? Loader { get; set; }

    /// <summary>Opens a link; set by the page.</summary>
    internal ILauncher Launcher { get; set; } = NullLauncher.Instance;

    /// <summary>Gets a value indicating whether the panel shows.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Gets a value indicating whether the read is in flight.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Gets a value indicating whether the read failed.</summary>
    public bool IsFailed { get; private set; }

    /// <summary>Gets the key of the node the panel is about; null when closed.</summary>
    internal string? Key { get; private set; }

    /// <summary>Gets the node's kind, as served; empty until read.</summary>
    public string Kind { get; private set; } = string.Empty;

    /// <summary>Gets the node's label; empty until read.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the source line ("ticket: LIN-12"); empty when none is served.</summary>
    public string SourceText { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether a source is served.</summary>
    public bool HasSource => SourceText.Length > 0;

    /// <summary>Gets a value indicating whether the source has a link to open.</summary>
    public bool HasLink => _link is not null;

    /// <summary>Gets a value indicating whether the node has a source but nothing to open (said out loud, never hidden).</summary>
    public bool HasNoLink => HasSource && !HasLink;

    /// <summary>Gets the line shown when the source has no link.</summary>
    public string NoLinkText { get; } = Labels.ContextNodePanelNoLink;

    /// <summary>Gets the open-source button's accessible name, by the source's kind.</summary>
    public string OpenSourceName { get; private set; } = Labels.ContextNodePanelOpenSourceName(string.Empty);

    /// <summary>Gets the short description; empty when none is served.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether a description is served.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>Gets the links, grouped by band; empty bands are left out.</summary>
    public IReadOnlyList<NodeEdgeGroupViewModel> Groups { get; private set; } = [];

    /// <summary>Gets a value indicating whether the node has any link.</summary>
    public bool HasGroups => Groups.Count > 0;

    /// <summary>Gets the panel's name.</summary>
    public string PanelName { get; } = Labels.ContextNodePanelName;

    /// <summary>Gets the close button's words.</summary>
    public string CloseText { get; } = Labels.ContextNodePanelClose;

    /// <summary>Gets the loading line.</summary>
    public string LoadingText { get; } = Labels.ContextNodePanelLoading;

    /// <summary>Gets the failure line.</summary>
    public string FailedText { get; } = Labels.ContextNodePanelFailed;

    /// <summary>Gets the heading over the links.</summary>
    public string LinksText { get; } = Labels.ContextNodePanelLinks;

    /// <summary>Gets the open-source button's words.</summary>
    public string OpenSourceText { get; } = Labels.ContextNodePanelOpenSource;

    /// <summary>Opens the panel on a node and reads it.</summary>
    /// <param name="key">The node's key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the panel shows the answer or the failure.</returns>
    internal Task Open(string key, CancellationToken cancellationToken = default) => Reading = Read(key, cancellationToken);

    /// <summary>Gets the latest read, for a caller that wants to wait for it.</summary>
    internal Task Reading { get; private set; } = Task.CompletedTask;

    private async Task Read(string key, CancellationToken cancellationToken)
    {
        var version = ++_version;
        var known = _lookup(key);
        (Key, IsOpen, IsLoading, IsFailed) = (key, true, true, false);
        Clear();
        (Kind, Title) = known is { } k ? (k.Kind, k.Label) : (KindOf(key), key);
        Changed();
        Result<NodeView>? answer;
        try
        {
            answer = Loader is { } load ? await load(key, cancellationToken).ConfigureAwait(true) : null;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version != _version)
        {
            return;   // another node was opened, or the panel closed
        }

        if (answer is { IsSuccess: true } ok)
        {
            Fill(ok.Value);
            Changed();
            await ReadSections(ok.Value, version, cancellationToken).ConfigureAwait(true);
            return;
        }

        (IsLoading, IsFailed) = (false, true);
        Changed();
    }

    /// <summary>Closes the panel (the close button, Esc).</summary>
    /// <returns>True when it was open.</returns>
    internal bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        _version++;   // a read still in flight is stale now
        (Key, IsOpen, IsLoading, IsFailed) = (null, false, false, false);
        Clear();
        Changed();
        return true;
    }

    /// <summary>True when the system refused to open the source's link, until the next press succeeds or another node opens.</summary>
    public bool LaunchFailed => _launch.Failed;

    /// <summary>The short line shown when the launch failed: "Couldn't open the link" and the link to copy.</summary>
    public string LaunchFailedText => _launch.FailedText;

    /// <summary>Opens the source's link in the system's browser.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>When the system answered.</returns>
    internal async Task OpenSource(CancellationToken cancellationToken = default)
    {
        if (_link is { } link)
        {
            _ = await _launch.Open(link, cancellationToken).ConfigureAwait(true);
        }
    }

    private static string KindOf(string key) => key.IndexOf(':', StringComparison.Ordinal) is var i and > 0 ? key[..i] : string.Empty;

    private void Clear()
    {
        (Kind, Title, SourceText, Description, _link) = (string.Empty, string.Empty, string.Empty, string.Empty, null);
        (Groups, Sections) = ([], []);
        _launch.Reset();
    }

    private void Fill(NodeView view)
    {
        var node = view.Node;
        (IsLoading, IsFailed) = (false, false);
        (Kind, Title) = (node.Kind, node.Label);
        Description = node.Description?.Trim() ?? string.Empty;
        if (node.Source is { } source)
        {
            var text = string.IsNullOrWhiteSpace(source.Title) ? source.Ref : source.Title;
            SourceText = Labels.ContextNodePanelSource(source.Kind, text);
            _link = LinkOf(source.Url);
            OpenSourceName = Labels.ContextNodePanelOpenSourceName(source.Kind);
        }

        var rows = view.Out.Select(e => Row(e.To, e, outgoing: true)).Concat(view.In.Select(e => Row(e.From, e, outgoing: false)));
        Groups =
        [
            .. rows.GroupBy(r => r.Band)
                .OrderBy(g => g.Key)
                .Select(g => new NodeEdgeGroupViewModel(NodeBands.Title(g.Key), [.. g.Select(r => r.Row)])),
        ];
    }

    private (WholeBand Band, NodeEdgeRowViewModel Row) Row(string other, NodeEdge edge, bool outgoing)
    {
        var known = _lookup(other);
        var kind = edge.OtherKind ?? known?.Kind ?? KindOf(other);
        var label = edge.OtherLabel ?? known?.Label ?? other;
        return (NodeBands.Of(kind), new NodeEdgeRowViewModel(other, Labels.ContextNodePanelEdge(label, edge.Type, outgoing), label, kind, _jump));
    }

    // Only a web link is opened; any other scheme is shown as the plain reference.
    private static Uri? LinkOf(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)) ? uri : null;

    private void Changed()
    {
        foreach (var name in (string[])[nameof(IsOpen), nameof(IsLoading), nameof(IsFailed), nameof(Kind), nameof(Title), nameof(SourceText), nameof(HasSource), nameof(HasLink), nameof(HasNoLink), nameof(OpenSourceName), nameof(LaunchFailed), nameof(LaunchFailedText), nameof(Description), nameof(HasDescription), nameof(Groups), nameof(HasGroups), nameof(Sections), nameof(HasSections)])
        {
            OnChanged(name);
        }
    }
}
