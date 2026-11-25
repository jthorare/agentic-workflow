using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a 'memory_write' node asks <see cref="IGraphMemoryPort"/> to write.</summary>
/// <param name="Run">The run whose node writes; the fact's provenance names its workflow, the run and its item.</param>
/// <param name="Memory">The memory's name.</param>
/// <param name="Fact">The fact's text.</param>
/// <param name="Tags">The tags; none is an empty list.</param>
/// <param name="ExpiresIn">How long the fact lives from now, or null for never.</param>
public sealed record MemoryToRemember(WorkflowRunId Run, string Memory, string Fact, IReadOnlyList<string> Tags, TimeSpan? ExpiresIn);
