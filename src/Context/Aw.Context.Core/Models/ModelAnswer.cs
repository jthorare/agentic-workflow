namespace Aw.Context.Core.Models;

/// <summary>A model's answer, with the tokens it cost (for the labeling caps).</summary>
/// <param name="Text">The answer text (untrusted until a human accepts what it produces).</param>
/// <param name="InputTokens">Tokens sent.</param>
/// <param name="OutputTokens">Tokens received.</param>
/// <param name="Model">The model the port asked for, or null when it left the choice to the agent CLI.</param>
public sealed record ModelAnswer(string Text, int InputTokens, int OutputTokens, string? Model = null)
{
    /// <summary>The harness that answered ('claude-code', 'codex', 'cursor'), or null when the port doesn't say (spec §7.1).</summary>
    public string? Harness { get; init; }

    /// <summary>Whether the token counts were reported by the CLI or estimated (spec §7.3).</summary>
    public TokenBasis TokenBasis { get; init; } = TokenBasis.Estimated;
}
