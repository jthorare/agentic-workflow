using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.StopSession;

/// <summary>What a stop answers.</summary>
/// <param name="Id">The row id the start answered: 'sindri-&lt;actId&gt;'.</param>
/// <param name="ActId">The key the start used.</param>
/// <param name="State">'ended'.</param>
public sealed record StoppedSession(SessionId Id, ActId ActId, string State);
