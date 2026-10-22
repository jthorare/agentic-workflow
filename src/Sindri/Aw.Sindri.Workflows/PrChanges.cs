using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What changed on a pull request between two polls (PR babysitter spec §6.1): the outcome is the first change in the table's order, and every change is listed.</summary>
internal static class PrChanges
{
    /// <summary>The changes of a poll against the last one.</summary>
    /// <param name="before">What the poller saw last, or null for a pull request it sees the first time.</param>
    /// <param name="now">The digest now.</param>
    /// <returns>The changes in table order ('merged', 'closed', 'updated', 'review', 'ci'); empty when nothing changed. A first sight changes only when the pull request is already merged or closed.</returns>
    public static IReadOnlyList<string> Between(PrWatchEntry? before, PullDigestRow now) => throw new NotSupportedException();
}
