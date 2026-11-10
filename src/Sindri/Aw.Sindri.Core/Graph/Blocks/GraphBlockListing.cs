using Aw.Sindri.Core.Graph.Docs;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The blocks of a catalog grouped as the picker shows them (Triggers, Data, AI, Logic, Outputs).</summary>
public static class GraphBlockListing
{
    /// <summary>Lists the blocks by group.</summary>
    /// <param name="catalog">The block types.</param>
    /// <param name="pages">The doc pages; a block's example spec is the first json example of its 'block-&lt;type&gt;' page.</param>
    /// <returns>The groups in picker order, each with its blocks ordered by title; a group with no block is left out.</returns>
    public static IReadOnlyList<GraphBlockGroup> Group(IGraphBlockCatalog catalog, IReadOnlyList<WorkflowDocPage> pages)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(pages);
        var examples = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            if (page.BlockType is { } type)
            {
                var json = page.JsonExamples();
                examples[type] = json.Count > 0 ? json[0] : null;
            }
        }

        return
        [
            .. catalog.Types.Values
                .Select(b => new GraphBlockEntry(b, examples.GetValueOrDefault(b.Type)))
                .GroupBy(static e => e.Group)
                .OrderBy(static g => g.Key)
                .Select(static g => new GraphBlockGroup(g.Key, [.. g.OrderBy(static e => e.Title, StringComparer.Ordinal)])),
        ];
    }
}
