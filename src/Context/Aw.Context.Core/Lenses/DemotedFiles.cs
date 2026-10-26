using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

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

    /// <summary>How many of <paramref name="keys"/> are demoted files, per role.</summary>
    /// <param name="keys">The code nodes the answer carries.</param>
    /// <param name="repos">The repos in scope.</param>
    /// <returns>The counts, or null when none is demoted (the answer leaves the block out).</returns>
    public static FilteredCounts? Counts(IEnumerable<NodeKey> keys, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(repos);
        var perRole = keys.Select(k => DemotedCode.Role(k, repos)).OfType<string>().GroupBy(role => role, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return perRole.Count == 0 ? null : new FilteredCounts(perRole);
    }

    /// <summary>How many of the band's files are demoted, per role.</summary>
    /// <param name="bands">The bands.</param>
    /// <param name="repos">The repos in scope.</param>
    /// <returns>The counts, or null when none is demoted.</returns>
    public static FilteredCounts? Counts(LensBands bands, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(bands);
        return Counts(bands.Code.Files.Where(f => f.Demoted).Select(f => NodeKey.From(CodePrefix + f.Path)), repos);
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

    /// <summary>The view with its demoted files last and the per-role count of them.</summary>
    /// <param name="view">The view.</param>
    /// <param name="repos">The repos in scope.</param>
    /// <returns>The view.</returns>
    public static LensesView Last(LensesView view, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(view);
        var bands = Last(view.Bands, repos);
        return view with { Bands = bands, Filtered = Counts(bands, repos) };
    }

    /// <summary>The set view with its demoted files last and the per-role count of them.</summary>
    /// <param name="view">The view.</param>
    /// <param name="repos">The repos in scope.</param>
    /// <returns>The view.</returns>
    public static LensesSetView Last(LensesSetView view, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(view);
        var bands = Last(view.Bands, repos);
        return view with { Bands = bands, Filtered = Counts(bands, repos) };
    }
}
