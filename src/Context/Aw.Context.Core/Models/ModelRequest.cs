namespace Aw.Context.Core.Models;

/// <summary>One model call. The prompt is already scrubbed: nothing unscrubbed ever reaches the port.</summary>
/// <param name="Purpose">Why it is asked.</param>
/// <param name="Prompt">The scrubbed prompt.</param>
/// <param name="MaxTokens">The most output tokens it may spend.</param>
/// <param name="Budget">How long it may take.</param>
/// <param name="Tier">The tier the caller picked, or null to leave the model to the port (every caller but the concept scan).</param>
/// <param name="Models">The model to ask each agent CLI for at <paramref name="Tier"/>, by CLI key ('claude', 'codex', 'cursor'); a CLI with no entry runs its own default. Null for every caller but the concept scan.</param>
public sealed record ModelRequest(ModelPurpose Purpose, string Prompt, int MaxTokens, TimeSpan Budget, ModelTier? Tier = null, IReadOnlyDictionary<string, string>? Models = null);
