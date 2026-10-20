namespace Aw.Sindri.Core.Ports;

/// <summary>One prompt to a model.</summary>
/// <param name="Role">Whose model answers: 'drafter', 'challenger', 'grader' or 'adjudicator' (W8's roles; until their profile keys exist every role is the default model).</param>
/// <param name="Prompt">The whole prompt text.</param>
/// <param name="Budget">How long the call may take.</param>
public sealed record PromptAsk(string Role, string Prompt, TimeSpan Budget);
