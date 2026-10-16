using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// What an effect's dispatch did (spec §5.7, §12; engine P6): the outcome of the reply event the 'RunEffect' activity
/// appends for a recipe effect ('notify.delivered' with 'delivered'; 'claim.created' with 'created' or 'claimExists'),
/// caused by the effect's dispatched row so the bridge routes it to this instance only.
/// </summary>
/// <param name="Reply">The reply's outcome; null when the effect replies with nothing (an engine notice signals nothing).</param>
/// <param name="Existed">The identity was already performed (an activity retry or a re-emission): nothing was done twice.</param>
public sealed record EffectDone(OutcomeName? Reply, bool Existed);
