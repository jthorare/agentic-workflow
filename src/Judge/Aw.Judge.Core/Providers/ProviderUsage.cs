namespace Aw.Judge.Core.Providers;

/// <summary>The tokens an agent CLI reported for one answer, as it counted them; nothing is estimated here.</summary>
/// <param name="InputTokens">The input tokens.</param>
/// <param name="OutputTokens">The output tokens.</param>
public sealed record ProviderUsage(long InputTokens, long OutputTokens)
{
    /// <summary>The model the CLI says it ran (Claude's resolved id, e.g. 'claude-haiku-5-5' for 'haiku'); null when it doesn't say.</summary>
    public string? Model { get; init; }
}
