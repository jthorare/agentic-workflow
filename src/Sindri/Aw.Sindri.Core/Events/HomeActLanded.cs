using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The source accepted an action (home spec §5.2).</summary>
/// <param name="Id">The row.</param>
/// <param name="ActId">The action's idempotency id.</param>
public sealed record HomeActLanded(HomeRowId Id, ActId ActId);
