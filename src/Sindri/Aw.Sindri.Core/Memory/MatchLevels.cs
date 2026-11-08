using Aw.Kernel;

namespace Aw.Sindri.Core.Memory;

/// <summary>The rules of <see cref="MatchLevel"/>.</summary>
public static class MatchLevels
{
    /// <summary>Gets the level a search uses when it names none.</summary>
    public static MatchLevel Default => MatchLevel.Balanced;

    /// <summary>The lowest cosine similarity (provisional: tuned on hand-made vectors, not yet on the pinned model's real output) a fact may have to be recalled at a level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The floor, from 0 to 1; a higher level has a higher floor.</returns>
    public static float Threshold(MatchLevel level) => level switch
    {
        MatchLevel.Loose => 0.15f,
        MatchLevel.Balanced => 0.5f,
        MatchLevel.Strict => 0.75f,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "not a match level"),
    };

    /// <summary>Reads a level from its wire name ('loose', 'balanced', 'strict').</summary>
    /// <param name="text">The name as typed; null or empty is <see cref="Default"/>.</param>
    /// <returns>The level, or SND-MEM-005.</returns>
    public static Result<MatchLevel> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Default;
        }

        return text.Trim().ToLowerInvariant() switch
        {
            "loose" => MatchLevel.Loose,
            "balanced" => MatchLevel.Balanced,
            "strict" => MatchLevel.Strict,
            _ => SindriErrors.MemoryMatchLevelInvalid.With("A match level is loose, balanced or strict."),
        };
    }

    /// <summary>The level's wire name.</summary>
    /// <param name="level">The level.</param>
    /// <returns>'loose', 'balanced' or 'strict'.</returns>
    public static string Wire(MatchLevel level) => level switch
    {
        MatchLevel.Loose => "loose",
        MatchLevel.Balanced => "balanced",
        MatchLevel.Strict => "strict",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "not a match level"),
    };
}
