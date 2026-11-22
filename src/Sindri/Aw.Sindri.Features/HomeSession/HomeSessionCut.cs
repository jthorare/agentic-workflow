using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Home;

namespace Aw.Sindri.Features.HomeSession;

/// <summary>
/// What an agent session sees of Home (home spec §9): only its own repo's rows, and of a row with no repo (a DM, a mention, a thread reply, a keyword hit, an agent at its limit,
/// a system row) no body, only that it exists. The owner sees every row whole.
/// </summary>
internal static class HomeSessionCut
{
    /// <summary>The scope a read of <paramref name="caller"/> pages through.</summary>
    /// <param name="caller">The caller.</param>
    /// <returns>The session's scope, or null for anyone else.</returns>
    public static HomeScope? ScopeOf(CallerContext caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return caller.Class == CallerClass.Session ? new HomeScope(caller.Repo) : null;
    }

    /// <summary>Whether <paramref name="caller"/> may read <paramref name="row"/> at all; a row it may not is not found, as if it were not there.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="row">The row.</param>
    /// <returns>False for a session and another repo's row.</returns>
    public static bool Sees(CallerContext caller, HomeRow row) => ScopeOf(caller)?.Sees(row) ?? true;

    /// <summary>The row as <paramref name="caller"/> may read it.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="row">The row.</param>
    /// <returns>The row itself for the owner or a row with a repo; for a session a row of no repo without its title, facts, blocks list and links.</returns>
    public static HomeRow For(CallerContext caller, HomeRow row)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(row);
        return caller.Class == CallerClass.Session && HomeScope.RepoLess(row.Kind)
            ? row with { Title = string.Empty, Facts = [], BlocksList = [], Links = [] }
            : row;
    }
}
