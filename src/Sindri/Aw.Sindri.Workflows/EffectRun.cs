using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'RunEffect' activity runs (engine spec §5.7; engine P6 unit (a)).</summary>
/// <param name="WorkflowId">The workflow id: the instance address.</param>
/// <param name="Item">The work item.</param>
/// <param name="Attempt">The instance's attempt.</param>
/// <param name="Command">The effect, its stable identity, origin and arguments.</param>
/// <param name="CommandId">This emission's command id (new on each re-emission; the identity stays).</param>
/// <param name="Text">An engine notice's rendered text; null for a recipe effect.</param>
/// <param name="Facts">The instance's facts when the effect was emitted: a '{ fact }' argument reads its value here.</param>
/// <param name="Recipe">The instance's recipe version: each attempt re-checks its approval and the allowlist (a later revoke is honoured); null skips the check.</param>
internal sealed record EffectRun(
    string WorkflowId,
    string Item,
    int Attempt,
    EffectCommand Command,
    string CommandId,
    string? Text,
    IReadOnlyDictionary<FactKey, FactValue> Facts,
    RecipeVersion? Recipe = null);
