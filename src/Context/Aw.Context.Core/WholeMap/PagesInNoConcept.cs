using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>The pages (<see cref="PageKinds.IsPage"/>) that relate to no concept: no 'expresses' edge from the page or from any action 'on' it (pages spec §3.4).</summary>
public static class PagesInNoConcept
{
    /// <summary>The keys of those pages, in ordinal key order.</summary>
    /// <param name="graph">The graph a caller may see.</param>
    /// <returns>The page keys; the count is the list's length.</returns>
    public static IReadOnlyList<string> Of(GraphSnapshot graph) => throw new NotSupportedException();
}
