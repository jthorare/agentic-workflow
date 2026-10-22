namespace Aw.Sindri.Core.Judge;

/// <summary>The judge's answer to a choose call, read from its envelope.</summary>
/// <param name="Status">How the call ended.</param>
/// <param name="Choice">The chosen option's name when decided; otherwise null.</param>
/// <param name="Reason">Why: the judge's reason code when decided or escalated (for example 'model', 'below-threshold'), the failure in words when failed.</param>
/// <param name="Confidence">The confidence, 0 to 1, when decided; otherwise null.</param>
/// <param name="Provider">The provider that decided ('jev', 'claude-cli', 'rules'…) when decided; otherwise null.</param>
/// <param name="DecisionRef">The id of the decision row the judge recorded (explain it with 'judge why'); null when the call failed before one was written.</param>
/// <param name="Probabilities">What the providers sent per option, when they sent any.</param>
public sealed record ChooseAnswer(
    ChooseStatus Status,
    string? Choice,
    string Reason,
    double? Confidence = null,
    string? Provider = null,
    string? DecisionRef = null,
    IReadOnlyDictionary<string, double>? Probabilities = null);
