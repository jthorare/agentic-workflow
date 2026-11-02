using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What the exclusions step is told (#1114) and where to look (issue 1005: the prefiltered graph and the context tools first, grep only to confirm): it answers SCP-COV-003 findings only, grouped, and changes nothing else in the map.</summary>
internal sealed class ScopeExcludeInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.scope.exclude");

    private static readonly string[] _lines =
    [
        "fact:findings lists SCP-COV-003 findings only: code-lens files near an anchor that fact:scopeMap neither maps nor excludes. Group them by directory and decide each group.",
        "Read graph.json in your run folder first: its size and limits say how big it is and what was cut. Work it with context_node, context_neighbors and context_query; they are scoped to that graph.",
        "Use grep or the checkout only to confirm what the graph shows. An id outside the graph answers not-in-graph; to step outside, pass outside: true with a reason. Every such step is logged, and the reason is read.",
        "Add to scopeMap.excluded one entry with a reason for each group you leave out, as {\"glob\": \"src/gen/**\", \"reason\": \"...\"} (or {\"item\": <key>, \"reason\": \"...\"} for one file); add a surface and map the file to it when it belongs in the scope.",
        "Change nothing else: not the acceptance list, not the other map fields, not an existing entry. fact:roundLog shows what earlier rounds already decided.",
        "fact:roundDigest (roundDigest.json) says what earlier rounds found and what has been settled: read its open findings first, they are what nothing has answered yet; a closed one is covered by an exclusion, a map row or an answer, so do not redo it. fact:roundLog is the raw record.",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
