namespace Aw.Context.Core.Models;

/// <summary>A model's answer, with the tokens it cost (for the labeling caps).</summary>
/// <param name="Text">The answer text (untrusted until a human accepts what it produces).</param>
/// <param name="InputTokens">Tokens sent.</param>
/// <param name="OutputTokens">Tokens received.</param>
/// <param name="Model">The model the port asked for, or null when it left the choice to the agent CLI.</param>
public sealed record ModelAnswer(string Text, int InputTokens, int OutputTokens, string? Model = null);
