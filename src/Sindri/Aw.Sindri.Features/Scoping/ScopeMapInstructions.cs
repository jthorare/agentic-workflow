using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What the Steps that write the scope map and acceptance list are told about their shape (#1112): both schemas are closed, so a field they do not list is refused when the agent reports.</summary>
internal sealed class ScopeMapInstructions : IAgentStepInstructions
{
    private static readonly BlockId[] _steps = [BlockId.From("step.scope.draft"), BlockId.From("step.interview.apply")];

    private static readonly string[] _lines =
    [
        $"fact:scopeMap is checked against {"schemas/scoping/scope-map.json"} and fact:acceptance against {"schemas/scoping/acceptance.json"}; neither allows a field the schema does not list, and a report with one is refused with its path.",
        "fact:scopeMap's top-level fields are only: surfaces, implications, workstreams, flagged, excluded, unmapped, unresolved. fact:acceptance's is only: items. Do not add others (no \"decisions\", no \"answers\").",
        "The owner's answers live in fact:answerLog, which Sindri writes: never copy them into the map. What an answer means for the work goes in scopeMap.implications, as an item that cites the answer (A<n>).",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => Array.IndexOf(_steps, step) >= 0;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
