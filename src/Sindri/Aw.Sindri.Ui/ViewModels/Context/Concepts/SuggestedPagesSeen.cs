using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>
/// Which suggested pages of each concept the owner has seen (pages spec §3.5), kept by concept key ("&lt;repo&gt;/&lt;concept id&gt;") as the page keys seen. A concept read for the first time sets its
/// baseline (nothing is new); a later read counts the keys not seen and forgets the seen keys the concept no longer lists; opening the concept's Pages section marks its current keys seen.
/// </summary>
internal static class SuggestedPagesSeen
{
    /// <summary>What reading the concepts gives: the seen store to keep and, per concept id, how many suggested pages are new.</summary>
    /// <param name="Seen">The store to write back.</param>
    /// <param name="New">The count per concept id, only for concepts with some new.</param>
    internal sealed record Reading(IReadOnlyDictionary<string, IReadOnlyList<string>> Seen, IReadOnlyDictionary<string, int> New);

    /// <summary>The key of a concept in the store.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="conceptId">The concept's id.</param>
    /// <returns>"repo/concept id".</returns>
    internal static string KeyOf(string repo, string conceptId) => repo + "/" + conceptId;

    /// <summary>Reads the repo's concepts against the store.</summary>
    /// <param name="seen">The store as it is.</param>
    /// <param name="repo">The repo.</param>
    /// <param name="concepts">The concepts the server listed.</param>
    /// <returns>The store to keep and the new counts.</returns>
    internal static Reading Read(IReadOnlyDictionary<string, IReadOnlyList<string>> seen, string repo, IReadOnlyList<ContextConcept> concepts)
    {
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(concepts);
        var next = new Dictionary<string, IReadOnlyList<string>>(seen, StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var concept in concepts)
        {
            var key = KeyOf(repo, concept.Id);
            var current = concept.SuggestedPages ?? [];
            if (!seen.TryGetValue(key, out var stored))
            {
                next[key] = current;   // the baseline: nothing is new the first time a concept is read
                continue;
            }

            var known = stored.ToHashSet(StringComparer.Ordinal);
            var listed = current.ToHashSet(StringComparer.Ordinal);
            var fresh = listed.Count(k => !known.Contains(k));
            if (fresh > 0)
            {
                counts[concept.Id] = fresh;
            }

            next[key] = [.. stored.Where(listed.Contains)];   // a key the concept no longer lists is forgotten; new ones wait until the section is opened
        }

        return new Reading(next, counts);
    }

    /// <summary>The store after the owner opened a concept's Pages section.</summary>
    /// <param name="seen">The store as it is.</param>
    /// <param name="key">The concept's key in the store.</param>
    /// <param name="current">The concept's suggested page keys now.</param>
    /// <returns>The store with the concept's current keys seen.</returns>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> Opened(IReadOnlyDictionary<string, IReadOnlyList<string>> seen, string key, IReadOnlyList<string> current)
    {
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(current);
        return new Dictionary<string, IReadOnlyList<string>>(seen, StringComparer.Ordinal) { [key] = [.. current.Distinct(StringComparer.Ordinal)] };
    }
}
