using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// What an effect runs from (spec §2, §5.7; engine P6). It names the instance and item by their text, so this namespace
/// stays below the engine's.
/// </summary>
/// <param name="Effect">The effect's id ('effect.notify.desk', 'effect.claim.ref').</param>
/// <param name="Identity">Its stable effect identity ('{instance}:{entry}:{index}', spec §5.7): what its existence check keys on.</param>
/// <param name="Command">This emission's command id: a 'retry' re-emission or a resumed dispatch gets a new one, the identity stays.</param>
/// <param name="FromEngine">Origin 'engine' (the engine's own notices: exempt from the recheck, and it signals nothing) or 'recipe'.</param>
/// <param name="Instance">The instance address ('T-4/dev-notify/1').</param>
/// <param name="Item">The item key ('T-4').</param>
/// <param name="Attempt">The instance's attempt.</param>
/// <param name="Arguments">Its 'with:' arguments, resolved to values.</param>
/// <param name="Facts">The instance's facts, by key, as stored values.</param>
/// <param name="Text">The notice's text: an engine notice's rendered line; null for a recipe effect, whose block builds its own.</param>
public sealed record EffectContext(
    BlockId Effect,
    string Identity,
    string Command,
    bool FromEngine,
    string Instance,
    string Item,
    int Attempt,
    IReadOnlyDictionary<string, JsonElement> Arguments,
    IReadOnlyDictionary<FactKey, JsonElement> Facts,
    string? Text);
