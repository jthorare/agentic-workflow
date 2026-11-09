namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One arrow of the graph: from the right-middle of one card to the left-middle of the next.</summary>
internal sealed class GraphEdgeViewModel
{
    internal GraphEdgeViewModel(string from, string to, string? label, GraphPoint start, GraphPoint end)
    {
        From = from;
        To = to;
        Label = label;
        Start = start;
        End = end;
    }

    /// <summary>The source node's id.</summary>
    public string From { get; }

    /// <summary>The target node's id.</summary>
    public string To { get; }

    /// <summary>The branch output ("true", "false") the edge leaves by, or null for a plain edge.</summary>
    public string? Label { get; }

    /// <summary>Where the arrow leaves.</summary>
    public GraphPoint Start { get; }

    /// <summary>Where the arrow head lands.</summary>
    public GraphPoint End { get; }
}
