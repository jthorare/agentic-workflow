namespace Aw.Context.Core.Concepts;

/// <summary>
/// A concepts answer as it is kept (issue 3676): the rows the map version and the profile decide, with no last change on them, and each row's code paths. A concept's last
/// change comes from git, outside the answer's key, so <see cref="With"/> lays it over the rows on every read and nothing kept ever holds a time.
/// </summary>
/// <param name="View">The answer, every row's LastChanged null.</param>
/// <param name="Code">The repo-relative code paths of each row's concept, by concept id.</param>
public sealed record KeptConcepts(ConceptsView View, IReadOnlyDictionary<string, IReadOnlyList<string>> Code)
{
    /// <summary>Gets the distinct code paths of every row, in ordinal order: what the read asks the change times for.</summary>
    public IReadOnlyList<string> Paths => [.. Code.Values.SelectMany(p => p).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>The answer with each row's last change: the newest known time of its code files, null when none is known.</summary>
    /// <param name="changed">The known change times by path.</param>
    /// <returns>The view to answer with.</returns>
    public ConceptsView With(IReadOnlyDictionary<string, DateTimeOffset> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        return changed.Count == 0 ? View : View with { Concepts = [.. View.Concepts.Select(r => r with { LastChanged = Newest(r, changed) })] };
    }

    private DateTimeOffset? Newest(ConceptRow row, IReadOnlyDictionary<string, DateTimeOffset> changed)
    {
        DateTimeOffset? newest = null;
        foreach (var path in Code.GetValueOrDefault(row.Id.Value) ?? [])
        {
            if (changed.TryGetValue(path, out var at) && (newest is null || at > newest))
            {
                newest = at;
            }
        }

        return newest;
    }
}
