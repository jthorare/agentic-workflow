using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// What an effect's dispatch did (spec §5.7, §12; engine P6): the outcome of the reply event the 'RunEffect' activity
/// appends for a recipe effect ('notify.delivered' with 'delivered'; 'claim.created' with 'created' or 'claimExists'),
/// caused by the effect's dispatched row so the bridge routes it to this instance only.
/// </summary>
/// <param name="Reply">The reply's outcome; null when the effect replies with nothing (an engine notice signals nothing).</param>
/// <param name="Existed">The identity was already performed (an activity retry or a re-emission): nothing was done twice.</param>
/// <param name="Event">The reply event's kind, for an outcome that more than one registered reply event declares ('merged'); null: the one event that declares <paramref name="Reply"/>.</param>
/// <param name="Facts">The facts the reply carries (the transition records them through its 'produces'); each must be one the effect's descriptor lists in 'Outputs' and the reply event declares, or the dispatch is refused. Null or empty: none.</param>
public sealed record EffectDone(OutcomeName? Reply, bool Existed, IReadOnlyDictionary<FactKey, ReplyFact>? Facts = null, EventKind? Event = null);
