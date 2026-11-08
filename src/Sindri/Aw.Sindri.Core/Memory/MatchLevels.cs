using Aw.Kernel;

namespace Aw.Sindri.Core.Memory;

/// <summary>The rules of <see cref="MatchLevel"/>.</summary>
public static class MatchLevels
{
    /// <summary>Gets the level a search uses when it names none.</summary>
    public static MatchLevel Default => MatchLevel.Balanced;

    /// <summary>The lowest cosine similarity a fact may have to be recalled at a level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The floor, from 0 to 1; a higher level has a higher floor.</returns>
    public static float Threshold(MatchLevel level) => throw new NotSupportedException();

    /// <summary>Reads a level from its wire name ('loose', 'balanced', 'strict').</summary>
    /// <param name="text">The name as typed; null or empty is <see cref="Default"/>.</param>
    /// <returns>The level, or SND-MEM-005.</returns>
    public static Result<MatchLevel> Parse(string? text) => throw new NotSupportedException();

    /// <summary>The level's wire name.</summary>
    /// <param name="level">The level.</param>
    /// <returns>'loose', 'balanced' or 'strict'.</returns>
    public static string Wire(MatchLevel level) => throw new NotSupportedException();
}
