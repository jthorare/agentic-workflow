namespace Aw.Sindri.Core.Line;

/// <summary>One decision the owner answered at stop 2, as C14's ingest takes it.</summary>
/// <param name="Text">What was decided: the question and the option picked.</param>
/// <param name="Nodes">The map nodes it is linked to (surfaces the decision cites that have a node key).</param>
/// <param name="Answer">The plan's answer log entry the pick comes from, for example 'A2'.</param>
public sealed record OwnerDecision(string Text, IReadOnlyList<string> Nodes, string Answer);
