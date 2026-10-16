using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// A recipe effect the recheck refused (spec §5.7, §3.8; engine P6 unit (b)), as the dispatch emitted it: what the
/// 'outbox.refused' trigger and the park it causes carry, so the park names the effect and 'answer --resume' re-sends
/// it under its identity without looking it up in the recipe, even after the instance has entered another frame.
/// </summary>
/// <param name="Kind">The effect block ('effect.notify.desk', 'effect.claim.ref').</param>
/// <param name="Identity">Its stable identity: a re-emission keeps it.</param>
/// <param name="With">Its arguments, as the recipe gave them.</param>
/// <param name="Origin">Who emitted it: a re-send keeps it, so an engine notice is re-sent as the engine's (interrupt class, its own text, no reply) and a recipe's effect is checked again like any recipe effect.</param>
public sealed record RefusedEffect(BlockId Kind, EffectIdentity Identity, IReadOnlyDictionary<string, WithValue> With, CommandOrigin Origin);
