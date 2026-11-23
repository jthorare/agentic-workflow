namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One node a connect may go to.</summary>
/// <param name="Id">The node's id.</param>
/// <param name="Text">What the list shows.</param>
internal sealed record ConnectTarget(string Id, string Text)
{
    /// <summary>The connect list for some steps, named the way their cards are (#3684): the card's line 1 (the title, or the block's name). Two steps that would read the
    /// same add their card's line 2; two that still read the same add their place in the list, "(2)". The id never shows: it lives only in the step sheet's Advanced group.</summary>
    /// <param name="nodes">The steps, in the order the list shows them.</param>
    /// <returns>One target per step.</returns>
    internal static IReadOnlyList<ConnectTarget> ListOf(IReadOnlyList<GraphNodeViewModel> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var titles = nodes.GroupBy(n => n.Title, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var texts = nodes.Select(n => titles[n.Title] > 1 && n.Line.Length > 0 ? Labels.WfEditTarget(n.Title, n.Line) : n.Title).ToList();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var counts = texts.GroupBy(t => t, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var list = new List<ConnectTarget>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            var text = texts[i];
            seen[text] = seen.GetValueOrDefault(text) + 1;
            list.Add(new ConnectTarget(nodes[i].Id, counts[text] > 1 ? Labels.WfEditTargetNth(text, seen[text]) : text));
        }

        return list;
    }
}
