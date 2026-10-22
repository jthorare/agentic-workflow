using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The context map (F3 spec 1): the lenses answer laid out on the world, as the nodes, edges, label pills and band frames the page
/// draws, plus the draft mark, the starting points and the coverage words of the control row. Built once per read; selection and
/// hover are in ContextMapViewModel.Selection.cs.
/// </summary>
internal sealed partial class ContextMapViewModel : ObservableObject
{

    /// <summary>The map of one answer.</summary>
    /// <param name="view">The lenses answer.</param>
    internal ContextMapViewModel(LensesView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Layout = ContextMapLayout.Build(view);
        Conflicts = view.Conflicts;
        _links = [.. Layout.Edges.Select(e => new MapLink(e.Key, e.From, e.To, e.Type))];
        Nodes = [.. Layout.Nodes.Select(n => new MapNodeViewModel(n))];
        Pills = [.. Layout.Edges.Select(e => new MapPillViewModel(e))];
        Edges = [.. Layout.Edges.Select(e => new MapEdge(e.Key, e.Route, e.Dashed))];
        Bands = [.. Layout.Bands.Select(b => MapBandViewModel.From(b, WorldWidth))];
        WorldHeight = Layout.World.Height;
        Draft = view.Draft;
        Anchors = [.. view.Anchors.Select(a => new MapAnchorViewModel(a.Key, a.Label, a.Kind))];
        (CoverageAreas, CoverageLead, CoverageRest) = CoverageWords.Words(view);
    }

    /// <summary>Gets the layout this map was drawn from.</summary>
    internal MapLayout Layout { get; }

    /// <summary>The conflicts between decisions, as served.</summary>
    internal IReadOnlyList<LensConflict> Conflicts { get; }

    /// <summary>The world's width, px.</summary>
    public double WorldWidth => Layout.World.Width;

    /// <summary>The world's height, px (taller when a row wrapped).</summary>
    public double WorldHeight { get; }

    /// <summary>The nodes, in reading order.</summary>
    public IReadOnlyList<MapNodeViewModel> Nodes { get; }

    /// <summary>The label pills, one per edge.</summary>
    public IReadOnlyList<MapPillViewModel> Pills { get; }

    /// <summary>The edges for the edges layer.</summary>
    internal IReadOnlyList<MapEdge> Edges { get; }

    /// <summary>The three band frames.</summary>
    public IReadOnlyList<MapBandViewModel> Bands { get; }

    /// <summary>Whether this is the draft map.</summary>
    public bool Draft { get; }

    /// <summary>The draft mark's word.</summary>
    public string DraftTag { get; } = Labels.ContextMapDraftTag;

    /// <summary>The starting points of the read.</summary>
    public IReadOnlyList<MapAnchorViewModel> Anchors { get; }

    /// <summary>The "N starting points" button's words.</summary>
    public string StartingPoints => Labels.ContextMapStartingPoints(Anchors.Count);

    /// <summary>The four areas of the coverage strip.</summary>
    internal IReadOnlyList<CoverageArea> CoverageAreas { get; }

    /// <summary>The coverage sentence's bold lead.</summary>
    public string CoverageLead { get; }

    /// <summary>The coverage sentence's rest.</summary>
    public string CoverageRest { get; }

    /// <summary>The node with a key, or null.</summary>
    /// <param name="key">The node's key.</param>
    /// <returns>The node.</returns>
    internal MapNodeViewModel? Node(string key) => Nodes.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.Ordinal));
}
