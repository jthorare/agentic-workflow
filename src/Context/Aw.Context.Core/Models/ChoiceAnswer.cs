namespace Aw.Context.Core.Models;

/// <summary>The answer to one <see cref="ChoiceQuestion"/>.</summary>
/// <param name="Key">The question's key.</param>
/// <param name="Picked">The option picked, exactly as offered, or null when the provider gave an answer that is none of them.</param>
/// <param name="Confidence">How sure the provider is, 0 to 1.</param>
public sealed record ChoiceAnswer(string Key, string? Picked, double Confidence);
