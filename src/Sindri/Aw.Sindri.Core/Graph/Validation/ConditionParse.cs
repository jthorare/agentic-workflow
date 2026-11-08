namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>What a 'condition.when' text parsed to: the reason it doesn't, or the references it reads.</summary>
/// <param name="Error">Why the text is not a condition, or null.</param>
/// <param name="References">The dotted references it reads ('agent.drifted'), empty on an error.</param>
public sealed record ConditionParse(string? Error, IReadOnlyList<string> References);
