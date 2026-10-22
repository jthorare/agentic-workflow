namespace Aw.Sindri.Core.Events;

/// <summary>A refreshed map draft is held until the code index catches up ('context.map.held', code index spec 6.4). Not public: it names the repo.</summary>
/// <param name="Repo">The version's own repo.</param>
/// <param name="Version">The held draft's number.</param>
/// <param name="Repos">How many repos the map covers.</param>
/// <param name="WaitingOn">How many of them are not yet built (and not absent).</param>
/// <param name="WaitMinutes">The bound, in minutes.</param>
/// <param name="Chained">True when the wait began with an earlier hold (a newer commit arrived; the wait did not restart).</param>
public sealed record ContextMapHeld(string Repo, int Version, int Repos, int WaitingOn, int WaitMinutes, bool Chained);
