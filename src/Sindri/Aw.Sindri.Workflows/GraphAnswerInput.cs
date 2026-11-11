namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'Answer' update of a <see cref="GraphWorkflow"/> carries: the answer to one 'ask_owner' node.</summary>
/// <param name="Node">The node's id.</param>
/// <param name="Text">A free-text answer, or null.</param>
/// <param name="Choice">A picked choice, or null.</param>
/// <param name="By">Who answers: the caller's actor: 'human:&lt;client&gt;', 'session:launch:&lt;launch id&gt;' or 'session:&lt;client&gt;'.</param>
internal sealed record GraphAnswerInput(string Node, string? Text, string? Choice, string By)
{
    /// <summary>The item inside a 'for_each' body the answer is for; 0 for a node outside a loop (items count from 1).</summary>
    public int Item { get; init; }
}
