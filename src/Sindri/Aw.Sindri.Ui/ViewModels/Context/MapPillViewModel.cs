using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One edge's label pill: the words, the point on the path it is centred on, and when it shows.</summary>
internal sealed class MapPillViewModel : ObservableObject
{
    /// <summary>A pill from its edge.</summary>
    /// <param name="edge">The edge it labels.</param>
    internal MapPillViewModel(MapEdgeLayout edge)
    {
        Edge = edge;
        Always = edge.CrossesBands || string.Equals(edge.Type, "conflicts", StringComparison.Ordinal);
        Opacity = Always ? 1 : 0;
    }

    /// <summary>The edge it labels.</summary>
    internal MapEdgeLayout Edge { get; }

    /// <summary>The edge's key.</summary>
    public string Key => Edge.Key;

    /// <summary>The words.</summary>
    public string Text => Edge.Label;

    /// <summary>The centre x on the world.</summary>
    public double X => Edge.MiddleX;

    /// <summary>The centre y on the world.</summary>
    public double Y => Edge.MiddleY;

    /// <summary>Whether the edge is suggested (the pill's border is dashed).</summary>
    public bool Dashed => Edge.Dashed;

    /// <summary>Whether the pill is always on (an edge between bands, or a conflict); in-band pills show on hover only.</summary>
    public bool Always { get; }

    /// <summary>How visible the pill is: an always-on one is 1 (dimmed with the rest while a node is lit); a hover-only one is 0 until an end of its edge is hovered or selected.</summary>
    public double Opacity
    {
        get;
        private set => Set(ref field, value);
    }

    /// <summary>Sets the pill's opacity for the highlight now lit.</summary>
    /// <param name="highlight">What is lit, or null when nothing is.</param>
    internal void Apply(MapHighlight? highlight) => Opacity = (highlight, Always) switch
    {
        (null, true) => 1,
        (null, false) => 0,
        ({ } lit, _) when !lit.DimsEdge(Key) => 1,
        (_, true) => MapHighlightStyle.DimmedOpacity,
        _ => 0,
    };
}
