namespace Aw.Transcripts.Core;

/// <summary>The wire names of <see cref="TranscriptProvider"/>: 'claude-code', 'codex' and 'cursor'.</summary>
public static class TranscriptProviderNames
{
    /// <summary>The wire name of a provider.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>'claude-code', 'codex' or 'cursor'.</returns>
    public static string Of(TranscriptProvider provider) => provider switch
    {
        TranscriptProvider.ClaudeCode => "claude-code",
        TranscriptProvider.Codex => "codex",
        TranscriptProvider.Cursor => "cursor",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "not a transcript provider"),
    };

    /// <summary>Reads a wire name.</summary>
    /// <param name="name">'claude-code', 'codex' or 'cursor'.</param>
    /// <param name="provider">The provider, when the name is one.</param>
    /// <returns>Whether the name is a provider's.</returns>
    public static bool TryParse(string? name, out TranscriptProvider provider)
    {
        foreach (var candidate in Enum.GetValues<TranscriptProvider>())
        {
            if (string.Equals(Of(candidate), name, StringComparison.Ordinal))
            {
                provider = candidate;
                return true;
            }
        }

        provider = default;
        return false;
    }
}
