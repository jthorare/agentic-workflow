using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The sections of an action's or a page's panel (<see cref="NodePanelViewModel"/>, continued, pages spec §3.4). A page is a node of kind "surface" (an action
/// is "on" one); a page's concepts are its own "expresses" edges plus those of its first <see cref="ActionReadCap"/> actions, each read through <see cref="NodePanelViewModel.Loader"/>.
/// </summary>
internal sealed partial class NodePanelViewModel
{
    /// <summary>The most actions of a page read for their concepts.</summary>
    internal const int ActionReadCap = 40;

    /// <summary>Reads the intent lens around one node; null while the page has no connection. Set by the page.</summary>
    internal Func<string, CancellationToken, Task<Result<LensesView>?>>? WhyLoader { get; set; }

    /// <summary>Gets the action or page sections; empty for every other kind.</summary>
    public IReadOnlyList<NodeSectionViewModel> Sections { get; private set; } = [];

    /// <summary>Gets a value indicating whether the sections show.</summary>
    public bool HasSections => Sections.Count > 0;

    private static bool IsAction(string kind) => string.Equals(kind, "action", StringComparison.Ordinal);

    private static bool IsPage(string kind) => string.Equals(kind, "surface", StringComparison.Ordinal);

    private NodeEdgeRowViewModel Link(string key, string? label, string? kind)
    {
        var known = _lookup(key);
        var text = label ?? known?.Label ?? key;
        return new NodeEdgeRowViewModel(key, text, text, kind ?? known?.Kind ?? KindOf(key), _jump);
    }

    private List<NodeEdgeRowViewModel> Rows(IEnumerable<NodeEdge> edges, bool outgoing) =>
        [.. edges.Select(e => Link(outgoing ? e.To : e.From, e.OtherLabel, e.OtherKind)).DistinctBy(r => r.Key, StringComparer.Ordinal)];

    // What the sections are drawn from; Concepts and Why are null until their reads answer.
    private sealed record Draft(bool IsAction, NodeView View, List<NodeEdgeRowViewModel> Own, List<NodeEdgeRowViewModel> Actions, List<NodeEdgeRowViewModel> Code)
    {
        internal List<NodeEdgeRowViewModel>? Concepts { get; init; }

        internal List<NodeEdgeRowViewModel>? Why { get; init; }

        internal bool WhyFailed { get; init; }
    }

    // The sections of a read node, then the reads they wait for (the lens, and for a page its actions' concepts); a newer Open or a Close drops late answers.
    private async Task ReadSections(NodeView view, int version, CancellationToken cancellationToken)
    {
        var action = IsAction(view.Node.Kind);
        if (!action && !IsPage(view.Node.Kind))
        {
            return;
        }

        var own = Rows(view.Out.Where(e => e.Type == "expresses"), outgoing: true);
        var draft = new Draft(action, view, own, Rows(view.In.Where(e => e.Type == "on"), outgoing: false), Rows(view.In.Where(e => e.Type == "implements"), outgoing: false)) { Concepts = action ? own : null };
        Sections = Build(draft);
        Changed();
        var whyTask = ReadWhy(view.Node.Key, cancellationToken);
        var conceptsTask = action ? Task.FromResult(own) : ReadActionConcepts(own, draft.Actions, cancellationToken);
        List<NodeEdgeRowViewModel> concepts;
        (List<NodeEdgeRowViewModel>? Rows, bool Failed) why;
        try
        {
            (why, concepts) = (await whyTask.ConfigureAwait(true), await conceptsTask.ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version != _version)
        {
            return;
        }

        Sections = Build(draft with { Concepts = concepts, Why = why.Rows, WhyFailed = why.Failed });
        Changed();
    }

    private List<NodeSectionViewModel> Build(Draft d)
    {
        var first = d.IsAction
            ? NodeSectionViewModel.Of(Labels.ContextNodePanelPage, Rows(d.View.Out.Where(e => e.Type == "on"), outgoing: true), Labels.ContextNodePanelNoPage)
            : NodeSectionViewModel.Of(Labels.ContextNodePanelActions, d.Actions, Labels.ContextNodePanelNoActions);
        var relates = d.Concepts is { } concepts
            ? NodeSectionViewModel.Of(Labels.ContextNodePanelRelatesTo, concepts, d.IsAction ? Labels.ContextNodePanelNoConcept : Labels.ContextNodePanelNoActionConcept)
            : NodeSectionViewModel.Loading(Labels.ContextNodePanelRelatesTo);
        var why = d.WhyFailed ? NodeSectionViewModel.Failed(Labels.ContextNodePanelWhy, FailedText)
            : d.Why is { } rows ? NodeSectionViewModel.Of(Labels.ContextNodePanelWhy, rows, Labels.ContextNodePanelNoWhy)
            : NodeSectionViewModel.Loading(Labels.ContextNodePanelWhy);
        return [first, relates, why, NodeSectionViewModel.Of(Labels.ContextNodePanelCode, d.Code, Labels.ContextPagesNoCode, d.Code.Count > 0 ? Labels.ContextPagesCode : string.Empty)];
    }

    // The decisions, then the records, the intent lens serves for the node (the server widens an action's or page's scope to its page or actions).
    private async Task<(List<NodeEdgeRowViewModel>? Rows, bool Failed)> ReadWhy(string key, CancellationToken cancellationToken)
    {
        var answer = WhyLoader is { } load ? await load(key, cancellationToken).ConfigureAwait(true) : null;
        if (answer is not { IsSuccess: true } ok)
        {
            return (null, true);
        }

        var intent = ok.Value.Bands.Intent;
        return ([.. intent.Decisions.Select(d => Link(d.Node, d.Label, "decision")), .. intent.Records.Select(r => Link(r.Key, r.Label, "record"))], false);
    }

    // The union of a page's own concepts and its actions', actions read in parallel; an action that can't be read adds none.
    private async Task<List<NodeEdgeRowViewModel>> ReadActionConcepts(List<NodeEdgeRowViewModel> own, List<NodeEdgeRowViewModel> actions, CancellationToken cancellationToken)
    {
        if (Loader is not { } load)
        {
            return own;
        }

        var reads = await Task.WhenAll(actions.Take(ActionReadCap).Select(a => load(a.Key, cancellationToken))).ConfigureAwait(true);
        var more = reads.OfType<Result<NodeView>>().Where(r => r.IsSuccess).SelectMany(r => Rows(r.Value.Out.Where(e => e.Type == "expresses"), outgoing: true));
        return [.. own.Concat(more).DistinctBy(r => r.Key, StringComparer.Ordinal)];
    }
}
