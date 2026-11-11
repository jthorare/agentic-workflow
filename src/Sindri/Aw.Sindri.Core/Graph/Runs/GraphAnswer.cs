using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>An answer to an 'ask_owner' node's question, as <see cref="IGraphRunAnswerer"/> takes it.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The 'ask_owner' node.</param>
/// <param name="Text">A free-text answer, or null.</param>
/// <param name="Choice">A picked choice, or null.</param>
/// <param name="By">Who answers: the caller's actor: 'human:&lt;client&gt;', 'session:launch:&lt;launch id&gt;' or 'session:&lt;client&gt;'.</param>
public sealed record GraphAnswer(WorkflowRunId Run, NodeId Node, string? Text, string? Choice, string By)
{
    /// <summary>The item inside a 'for_each' body the answer is for, or null for a node outside a loop.</summary>
    public int? Item { get; init; }

    /// <summary>The workflow that waits for the answer when it is not the run's own (the question recorded its id); null for the run's own.</summary>
    public string? BatchWorkflow { get; init; }
}
