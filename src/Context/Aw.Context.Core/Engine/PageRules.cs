using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>The rules for named pages at write (pages spec §3.1): pure, over the pages and the current map's facts.</summary>
public static class PageRules
{
    /// <summary>Checks pages against the map.</summary>
    /// <param name="pages">The pages as the owner wrote them.</param>
    /// <param name="facts">The current map's concept ids, surface and action keys and their repos.</param>
    /// <returns>Every problem found, in page order; empty when the pages hold.</returns>
    public static IReadOnlyList<Error> Check(IReadOnlyList<PagePin> pages, PageFacts facts) =>
        throw new NotSupportedException("PageRules.Check is not built yet");
}
