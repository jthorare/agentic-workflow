using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Home;

/// <summary>What one collector pass writes (home spec §3.1): the rows to raise, the rows that changed and the rows to clear. Each becomes one 'home.row.*' event.</summary>
/// <param name="Raised">Rows the source has and Home does not.</param>
/// <param name="Changed">Rows whose why chip, waits-on, actions or links moved, each at its next version.</param>
/// <param name="Cleared">Rows Home holds whose trigger stopped holding.</param>
public sealed record HomePlan(IReadOnlyList<HomeRaise> Raised, IReadOnlyList<HomeRaise> Changed, IReadOnlyList<HomeRowId> Cleared)
{
    /// <summary>True when the pass writes nothing.</summary>
    public bool IsEmpty => Raised.Count == 0 && Changed.Count == 0 && Cleared.Count == 0;
}
