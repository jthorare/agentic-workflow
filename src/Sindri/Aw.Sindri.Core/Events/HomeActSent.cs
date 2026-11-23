using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>An action on a row went to its source (home spec §5.2). The event keeps a body hash only.</summary>
/// <param name="Id">The row.</param>
/// <param name="ActId">The action's idempotency id.</param>
/// <param name="Action">The action id.</param>
/// <param name="BodyHash">A hash of the redacted arguments.</param>
public sealed record HomeActSent(HomeRowId Id, ActId ActId, string Action, string BodyHash);
