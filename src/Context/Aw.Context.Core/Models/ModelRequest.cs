namespace Aw.Context.Core.Models;

/// <summary>One model call. The prompt is already scrubbed: nothing unscrubbed ever reaches the port.</summary>
/// <param name="Purpose">Why it is asked.</param>
/// <param name="Prompt">The scrubbed prompt.</param>
/// <param name="MaxTokens">The most output tokens it may spend.</param>
/// <param name="Budget">How long it may take.</param>
/// <param name="Tier">The tier the caller picked, or null to leave the model to the port (every caller but the concept scan).</param>
/// <param name="Models">The model to ask each agent CLI for at <paramref name="Tier"/>, by CLI key ('claude', 'codex', 'cursor'); a CLI with no entry there runs <paramref name="Model"/>, else its own default. Null for every caller but the concept scan.</param>
/// <param name="Model">One model for every agent CLI that has no entry in <paramref name="Models"/> (the model-pick question's answer for #1172); null runs the CLI's own default.</param>
public sealed record ModelRequest(ModelPurpose Purpose, string Prompt, int MaxTokens, TimeSpan Budget, ModelTier? Tier = null, IReadOnlyDictionary<string, string>? Models = null, string? Model = null)
{
    /// <summary>
    /// Set by a caller that sized its prompt for the smallest harness it could land on (spec §6.2): the router then skips, with outcome 'skipped' and reason 'prompt-too-long', any harness whose own prompt cap is below the prompt's length,
    /// rather than let it cut the prompt silently. Null leaves the harnesses as they are.
    /// </summary>
    public int? MaxPromptChars { get; init; }
}
