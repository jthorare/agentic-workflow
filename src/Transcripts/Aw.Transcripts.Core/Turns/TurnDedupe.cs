namespace Aw.Transcripts.Core.Turns;

/// <summary>
/// Decides which turns of a file are copies. A resumed or forked session copies earlier lines into a new file with the
/// same timestamps, so a turn whose <see cref="TurnKey"/> a file that sorts earlier (<see cref="EarlierFileFirst"/>)
/// also holds is a copy, and the corpus keeps only the earliest file's turn. A file never dedupes against itself.
/// </summary>
public static class TurnDedupe
{
    /// <summary>Which of a file's turns are its own.</summary>
    /// <param name="ownKeys">The file's turn keys, in turn order.</param>
    /// <param name="earlierKeys">The turn keys of every file that sorts before it.</param>
    /// <returns>For each turn, whether no earlier file holds it.</returns>
    public static IReadOnlyList<bool> Own(IReadOnlyList<string> ownKeys, IEnumerable<IReadOnlyList<string>> earlierKeys)
    {
        ArgumentNullException.ThrowIfNull(ownKeys);
        ArgumentNullException.ThrowIfNull(earlierKeys);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var keys in earlierKeys)
        {
            seen.UnionWith(keys);
        }

        return [.. ownKeys.Select(k => !seen.Contains(k))];
    }

    /// <summary>Whether a file that sits beside <paramref name="target"/> can hold copies of its turns: it was still being written when the target's first line was, and it sorts first.</summary>
    /// <param name="candidate">The other file's span.</param>
    /// <param name="candidateWrittenAt">The other file's last write time.</param>
    /// <param name="target">The target's span.</param>
    /// <returns>Whether to count the other file's turns as earlier.</returns>
    public static bool CanHoldOriginals(FileSpan candidate, DateTimeOffset candidateWrittenAt, FileSpan target)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(target);
        return target.First is { } first && candidateWrittenAt >= first && EarlierFileFirst.Instance.Compare(candidate, target) < 0;
    }
}
