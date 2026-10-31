using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Pages;

/// <summary>Matches pasted blocks against the map's pages and checks them against the page rules, without writing anything.</summary>
public static class PageListPreview
{
    /// <summary>Previews the blocks.</summary>
    /// <param name="blocks">The parsed paste.</param>
    /// <param name="pages">Every page the caller sees (owner and suggested).</param>
    /// <param name="concepts">Pins first, then the map's concepts, each (id, name).</param>
    /// <returns>One row per block.</returns>
    public static IReadOnlyList<PageListRow> Of(IReadOnlyList<PageListBlock> blocks, IReadOnlyList<PageRow> pages, IReadOnlyList<(ConceptId Id, string Name)> concepts) =>
        throw new NotSupportedException();
}
