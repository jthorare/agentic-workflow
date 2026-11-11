using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.answered' payload (issue 2258, PR 9): someone answered an 'ask_owner' node; the timeline names who.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The 'ask_owner' node.</param>
/// <param name="Answer">The answer.</param>
/// <param name="By">Who answered: the caller's actor: 'human:&lt;client&gt;' for a person, 'session:launch:&lt;launch id&gt;' for a session Sindri launched, 'session:&lt;client&gt;' for any other session.</param>
public sealed record WorkflowAnsweredPayload(WorkflowRunId Run, NodeId Node, string Answer, string By)
{
    /// <summary>The item the answered question was asked for inside a 'for_each' body, or null.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Item { get; init; }
}
