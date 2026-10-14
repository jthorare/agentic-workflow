using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The session repo filter (API spec §3.1, §5.2): a human sees every event; a session with a repo sees the events of its
/// repo's items and the events with no repo, and nothing from another repo.
/// </summary>
public static class EventVisibility
{
    /// <summary>True when <paramref name="caller"/> may see <paramref name="row"/>.</summary>
    /// <param name="caller">Who reads.</param>
    /// <param name="row">The ledger row.</param>
    /// <param name="repos">Where each item belongs.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Whether it is visible.</returns>
    public static ValueTask<Result<bool>> Visible(CallerContext caller, LedgerEvent row, IItemRepos repos, CancellationToken cancellationToken)
        => throw new NotSupportedException("EventVisibility.Visible: P5 unit D makes the session repo filter tests pass");
}
