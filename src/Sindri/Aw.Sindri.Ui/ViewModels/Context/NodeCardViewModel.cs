using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The inspector's node card (F3 spec 1 §2 "2e"): what the chosen node is, why it is in the map, a conflict it is part of, what it is
/// linked to, and Accept or Reject on a suggestion. The words come from the map the node is on; the links come from GET
/// /sindri/context/node?key=.
/// </summary>
internal sealed partial class NodeCardViewModel(ContextPorts ports) : ObservableObject
{
    private int _generation;
    private ContextMapViewModel? _map;
    private string? _key;

    /// <summary>What the stage reads through.</summary>
    internal ContextPorts Ports { get; } = ports;

    /// <summary>Whether a node is chosen.</summary>
    public bool IsOpen => Title.Length > 0;

    /// <summary>The node's label; empty when none is chosen.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>What it is, for the "What it is" section: the kind word and the label (the prose is not served yet).</summary>
    public string What { get; private set; } = string.Empty;

    /// <summary>The node's kind in plain words.</summary>
    public string KindWord { get; private set; } = string.Empty;

    /// <summary>The caption: the kind plus its technical detail.</summary>
    public string Caption { get; private set; } = string.Empty;

    /// <summary>The kind's icon in Theme/Icons.axaml, without the prefix.</summary>
    public string KindIcon { get; private set; } = string.Empty;

    /// <summary>How sure the map is, in words.</summary>
    public string Sure { get; private set; } = string.Empty;

    /// <summary>How the node is reached from the nearest starting point.</summary>
    public string Why { get; private set; } = string.Empty;

    /// <summary>Whether the node is part of a conflict.</summary>
    public bool HasConflict => ConflictBody.Length > 0;

    /// <summary>The conflict, in a sentence.</summary>
    public string ConflictBody { get; private set; } = string.Empty;

    /// <summary>The nodes this one is linked to.</summary>
    public IReadOnlyList<NodeLinkRow> Connected { get; private set; } = [];

    /// <summary>Whether nothing else is linked to the node (after the read).</summary>
    public bool NoneConnected { get; private set; }

    /// <summary>The refusal of the last read or send, as served, or empty.</summary>
    public string Problem { get; private set; } = string.Empty;

    /// <summary>Whether there is a refusal to show.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>The section titles and buttons.</summary>
    public string WhatTitle { get; } = Labels.ContextInspWhat;

    /// <summary>The "why" section's title.</summary>
    public string WhyTitle { get; } = Labels.ContextInspWhy;

    /// <summary>The conflict section's title.</summary>
    public string ConflictTitle { get; } = Labels.ContextInspConflict;

    /// <summary>The connected section's title.</summary>
    public string ConnectedTitle { get; } = Labels.ContextInspConnected;

    /// <summary>The connected section's line when nothing is linked.</summary>
    public string ConnectedNone { get; } = Labels.ContextInspConnectedNone;

    /// <summary>The accept button's word.</summary>
    public string AcceptWord { get; } = Labels.ContextInspAccept;

    /// <summary>The reject button's word.</summary>
    public string RejectWord { get; } = Labels.ContextInspReject;

    /// <summary>The copy button's word.</summary>
    public string CopyPathWord { get; } = Labels.ContextInspCopyPath;

    /// <summary>Whether the node is a file, so its path can be copied.</summary>
    public bool CanCopyPath { get; private set; }

    /// <summary>Where the record opens at its source, or null when it is not a cited record or has no link.</summary>
    public Uri? Link { get; private set; }

    /// <summary>Whether there is a link to open.</summary>
    public bool HasLink => Link is not null;

    /// <summary>Whether the open row (the button or the caption) shows at all.</summary>
    public bool HasOpenRow => HasLink || NoLink;

    /// <summary>The button's words ("Open in Linear").</summary>
    public string OpenText { get; private set; } = string.Empty;

    /// <summary>The button's name for a screen reader ("Open SPL-103 in Linear").</summary>
    public string OpenName { get; private set; } = string.Empty;

    /// <summary>Whether the record is cited but has no link to give.</summary>
    public bool NoLink { get; private set; }

    /// <summary>The caption shown when there is no link.</summary>
    public string NoLinkText { get; } = Labels.ContextNoLink;

    /// <summary>Opens the record at its source through the launcher.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>When the system answered.</returns>
    internal async Task OpenRecord(CancellationToken cancellationToken = default)
    {
        if (Link is { } link)
        {
            _ = await (Ports.Launcher ?? NullLauncher.Instance).OpenUri(link, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Chooses a node: its words at once, its links when the read returns.</summary>
    /// <param name="map">The map the node is on.</param>
    /// <param name="key">The node's key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the links are shown.</returns>
    internal async Task Show(ContextMapViewModel map, string key, CancellationToken cancellationToken = default)
    {
        var node = map.Node(key);
        if (node is null)
        {
            return;
        }

        var generation = ++_generation;   // a read for a node the person has moved off must not draw
        (_map, _key) = (map, key);
        Fill(map, node);
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var read = await Ports.Api.Node(api, key, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }

        if (read.IsSuccess)
        {
            Connected = [.. read.Value.Out.Select(e => Row(map, e.To, e)).Concat(read.Value.In.Select(e => Row(map, e.From, e)))];
            NoneConnected = Connected.Count == 0;
        }
        else
        {
            Problem = read.Error.Message;
        }

        Refresh();
    }

    /// <summary>Clears the card (Esc).</summary>
    internal void Close()
    {
        _generation++;
        (_map, _key) = (null, null);
        Title = What = KindWord = Caption = KindIcon = Sure = Why = ConflictBody = Problem = string.Empty;
        Connected = [];
        (NoneConnected, CanCopyPath) = (false, false);
        FillRecord(found: null);
        ResetReview();
        Refresh();
    }

    private void Fill(ContextMapViewModel map, MapNodeViewModel node)
    {
        var layout = node.Layout;
        Title = node.Label;
        What = node.Name;
        KindWord = Labels.ContextMapKindWord(layout.Kind);
        Caption = node.Caption;
        KindIcon = node.KindIcon;
        Sure = Labels.ContextInspSure(layout.Confirmed, layout.Provenance, layout.Confidence);
        Why = map.PathSentence(node.Key);
        ConflictBody = ConflictWords(map, node.Key);
        CanCopyPath = layout.Path.Length > 0;
        FillRecord(map.RecordFor(node.Key));
        Connected = [];
        NoneConnected = false;
        Problem = string.Empty;
        ResetReview();
        Refresh();
    }

    // Only a web link is opened; a record with a citation but no (web) link says so, one without a citation shows nothing.
    private void FillRecord((LensRecord Record, LensCitation? Citation)? found)
    {
        (Link, OpenText, OpenName, NoLink) = (null, string.Empty, string.Empty, false);
        if (found is not ({ } record, { } citation))
        {
            return;
        }

        var web = citation.Permalink is { IsAbsoluteUri: true } uri && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal));
        NoLink = !web;
        if (web)
        {
            var name = citation.Aliases is [var alias, ..] ? alias : record.Label;
            (Link, OpenText, OpenName) = (citation.Permalink, Labels.ContextOpenIn(record.Source), Labels.ContextOpenInName(name, record.Source));
        }
    }

    private static string ConflictWords(ContextMapViewModel map, string key)
    {
        if (map.ConflictsFor(key) is not [var conflict, ..])
        {
            return string.Empty;
        }

        string Label(string? k) => k is null ? string.Empty : map.Node(k)?.Label ?? k;
        if (string.Equals(conflict.Node, key, StringComparison.Ordinal))
        {
            return Labels.ContextInspConflictNode(Label(conflict.Decision), Label(conflict.Other));
        }

        var other = string.Equals(conflict.Decision, key, StringComparison.Ordinal) ? conflict.Other : conflict.Decision;
        return Labels.ContextInspConflictBody(Label(other), Label(conflict.Node));
    }

    private static NodeLinkRow Row(ContextMapViewModel map, string other, NodeEdge edge)
    {
        var node = map.Node(other);
        var kind = node is null ? string.Empty : " · " + Labels.ContextMapKindWord(node.Layout.Kind).ToLowerInvariant();
        return new NodeLinkRow(other, node?.Label ?? other, Labels.ContextMapEdgeWord(edge.Type) + kind + RollUp(edge) + (edge.Confirmed ? string.Empty : " · " + Labels.ContextInspNotConfirmed), !edge.Confirmed);
    }

    // "depends on · feature · 3 imports, e.g. a.ts -> b.ts": what a feature-to-feature edge sums over its files.
    private static string RollUp(NodeEdge edge) => edge.Count is { } count ? " · " + Labels.ContextMapRollUp(edge.Type, count, edge.Example) : string.Empty;

    private void Refresh()
    {
        foreach (var name in new[] { nameof(IsOpen), nameof(Title), nameof(What), nameof(KindWord), nameof(Caption), nameof(KindIcon), nameof(Sure), nameof(Why), nameof(HasConflict), nameof(ConflictBody), nameof(Connected), nameof(NoneConnected), nameof(Problem), nameof(HasProblem), nameof(CanCopyPath), nameof(Link), nameof(HasLink), nameof(OpenText), nameof(OpenName), nameof(NoLink), nameof(HasOpenRow) })
        {
            OnChanged(name);
        }
    }
}
