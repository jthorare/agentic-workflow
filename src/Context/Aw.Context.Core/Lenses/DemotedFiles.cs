using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>A file the repo demotes (<see cref="ContextRepo.Demotes"/>) keeps its node and a direct read of it, but ranks after every product file and is never an anchor or a suggestion.</summary>
public static class DemotedFiles
{
    private const string CodePrefix = "code:";

    /// <summary>The items with every demoted one moved after the rest; the order inside each group stays.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items, best first.</param>
    /// <param name="demoted">Whether an item is a demoted file's.</param>
    /// <returns>The items, demoted last.</returns>
    public static IReadOnlyList<T> Last<T>(IReadOnlyList<T> items, Func<T, bool> demoted)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(demoted);
        return [.. items.OrderBy(demoted)];   // OrderBy is stable and false sorts first
    }

    /// <summary>The band with its demoted files moved after the product files.</summary>
    /// <param name="band">The code band.</param>
    /// <param name="repos">The repos in scope.</param>
    /// <returns>The band.</returns>
    public static CodeBand Last(CodeBand band, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(band);
        ArgumentNullException.ThrowIfNull(repos);
        return repos.All(r => r.DemotePaths.Count == 0) ? band : band with { Files = Last(Flagged(band.Files, repos), f => f.Demoted) };
    }

    private static List<LensFile> Flagged(IReadOnlyList<LensFile> files, IReadOnlyList<ContextRepo> repos) =>
        [.. files.Select(f => DemotedCode.IsDemoted(NodeKey.From(CodePrefix + f.Path), repos) ? f with { Demoted = true } : f)];

    /// <summary>The bands with the code band's demoted files moved after the product files.</summary>
    /// <param name="bands">The bands.</param>
    /// <param name="repos">The repos in scope.</param>
    /// <returns>The bands.</returns>
    public static LensBands Last(LensBands bands, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(bands);
        return bands with { Code = Last(bands.Code, repos) };
    }
}
