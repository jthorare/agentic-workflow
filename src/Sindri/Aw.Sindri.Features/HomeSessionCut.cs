using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Home;

namespace Aw.Sindri.Features;

/// <summary>What an agent session sees of a Home row (home spec §8): a row with no repo of its own (a DM, a mention, a thread reply, a keyword hit) shows no body, only that it exists.</summary>
internal static class HomeSessionCut
{
    /// <summary>The row as <paramref name="caller"/> may read it.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="row">The row.</param>
    /// <returns>The row itself for the owner or a row with a repo; for a session a row of no repo without its title, facts, blocks list and links.</returns>
    public static HomeRow For(CallerContext caller, HomeRow row)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(row);
        return caller.Class == CallerClass.Session && row.Kind is HomeKind.Dm or HomeKind.Mention or HomeKind.ThreadReply or HomeKind.Keyword
            ? row with { Title = string.Empty, Facts = [], BlocksList = [], Links = [] }
            : row;
    }
}
