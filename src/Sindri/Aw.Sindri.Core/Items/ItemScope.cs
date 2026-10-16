using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Items;

/// <summary>Which items a caller may see (API spec §3.1, session repo scope).</summary>
public static class ItemScope
{
    /// <summary>True when <paramref name="caller"/> may see an item in <paramref name="itemRepo"/>: a human or service sees every item, a session only its repo's.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="itemRepo">The item's repo, or null.</param>
    /// <returns>True when visible.</returns>
    public static bool Visible(CallerContext caller, RepoName? itemRepo)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return caller.Class != CallerClass.Session || Equals(caller.Repo, itemRepo);
    }

    /// <summary>True when <paramref name="caller"/> may see a request, decision or needs-you row in <paramref name="repo"/> (API spec §3.1): a human or service sees every row, a session its repo's and the repo-less ones.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="repo">The row's repo, or null.</param>
    /// <returns>True when visible.</returns>
    public static bool VisibleOrRepoless(CallerContext caller, RepoName? repo)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return caller.Class != CallerClass.Session || repo is null || Equals(caller.Repo, repo);
    }
}
