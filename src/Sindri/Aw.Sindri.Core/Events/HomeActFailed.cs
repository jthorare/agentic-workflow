using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The source refused an action or did not answer (home spec §5.2).</summary>
/// <param name="Id">The row.</param>
/// <param name="ActId">The action's idempotency id.</param>
/// <param name="Reason">The source's reason, scrubbed.</param>
public sealed record HomeActFailed(HomeRowId Id, ActId ActId, string Reason);
