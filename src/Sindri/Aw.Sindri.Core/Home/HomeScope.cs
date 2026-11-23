using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Home;

/// <summary>
/// What an agent session sees of Home (home spec §9): the rows of the repo its X-Sindri-Repo header names, and every row of no repo (a DM, a mention, a thread reply,
/// a keyword hit, an agent at its limit, a system row), whose body the session never gets. A session with no repo sees only the rows of no repo.
/// </summary>
/// <param name="Repo">The session's repo, or null.</param>
public sealed record HomeScope(RepoName? Repo)
{
    /// <summary>Whether a row of <paramref name="kind"/> belongs to no repo (home spec §9).</summary>
    /// <param name="kind">The row's kind.</param>
    /// <returns>True for dm, mention, thread-reply, keyword, agent-limited and system.</returns>
    public static bool RepoLess(HomeKind kind) =>
        kind is HomeKind.Dm or HomeKind.Mention or HomeKind.ThreadReply or HomeKind.Keyword or HomeKind.AgentLimited or HomeKind.System;

    /// <summary>Whether the session sees <paramref name="row"/> at all.</summary>
    /// <param name="row">The row.</param>
    /// <returns>True for a row of no repo, or a row of the session's own repo.</returns>
    public bool Sees(HomeRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return RepoLess(row.Kind) || (row.Repo is { } repo && Repo is { } mine && string.Equals(repo.Value, mine.Value, StringComparison.Ordinal));
    }
}
