using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What the exclusions step is told (#1114): it answers SCP-COV-003 findings only, grouped, and changes nothing else in the map.</summary>
internal sealed class ScopeExcludeInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.scope.exclude");

    private static readonly string[] _lines =
    [
        "fact:findings lists SCP-COV-003 findings only: code-lens files near an anchor that fact:scopeMap neither maps nor excludes. Group them by directory and decide each group.",
        "Add to scopeMap.excluded one entry with a reason for each group you leave out, as {\"glob\": \"src/gen/**\", \"reason\": \"...\"} (or {\"item\": <key>, \"reason\": \"...\"} for one file); add a surface and map the file to it when it belongs in the scope.",
        "Change nothing else: not the acceptance list, not the other map fields, not an existing entry. fact:roundLog shows what earlier rounds already decided.",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
