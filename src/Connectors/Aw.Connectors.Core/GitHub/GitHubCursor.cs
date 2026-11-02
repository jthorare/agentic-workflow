namespace Aw.Connectors.Core.GitHub;

/// <summary>
/// Where an issue list read stands: the newest updated_at seen and the ids seen at exactly that instant (ConnectAI's boundary rule). GitHub's since
/// is inclusive, so the next run gets the entries at that instant again; the ids drop the ones already read, and the ones not yet read still come.
/// </summary>
/// <param name="UpdatedAt">The newest change time seen, or the window start for a first run.</param>
/// <param name="SeenIds">The ids already read whose change time is exactly <paramref name="UpdatedAt"/>.</param>
public sealed record GitHubCursor(DateTimeOffset UpdatedAt, IReadOnlyList<long> SeenIds)
{
    /// <summary>A cursor that has seen nothing, for a first run starting at <paramref name="since"/>.</summary>
    /// <param name="since">The window start.</param>
    /// <returns>The cursor.</returns>
    public static GitHubCursor From(DateTimeOffset since) => new(since, []);

    /// <summary>True when <paramref name="issue"/> has not been read: it changed after the cursor, or at the cursor's instant with an id not in <see cref="SeenIds"/>.</summary>
    /// <param name="issue">An entry from the list.</param>
    /// <returns>Whether to keep it.</returns>
    public bool Admits(GitHubIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return issue.UpdatedAt > UpdatedAt || (issue.UpdatedAt == UpdatedAt && !SeenIds.Contains(issue.Id));
    }

    /// <summary>The cursor after reading <paramref name="read"/>: the newest change time among the cursor and the entries, with the ids seen at it.</summary>
    /// <param name="read">The entries read since this cursor (all admitted).</param>
    /// <returns>The next cursor.</returns>
    public GitHubCursor Advance(IReadOnlyCollection<GitHubIssue> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var newest = read.Count == 0 ? UpdatedAt : read.Max(static i => i.UpdatedAt);
        if (newest < UpdatedAt)
        {
            newest = UpdatedAt;
        }

        var seen = new HashSet<long>(newest == UpdatedAt ? SeenIds : []);
        foreach (var issue in read.Where(i => i.UpdatedAt == newest))
        {
            _ = seen.Add(issue.Id);
        }

        return new GitHubCursor(newest, [.. seen.Order()]);
    }
}
