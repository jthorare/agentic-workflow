namespace Aw.Context.Infrastructure.Linear;

/// <summary>A project's newest update, as far as the feed reads it.</summary>
/// <param name="Health">The Health Linear serves (onTrack, atRisk, offTrack).</param>
/// <param name="CreatedAt">The CreatedAt Linear serves.</param>
internal sealed record LinearProjectHealth(string? Health = null, DateTimeOffset? CreatedAt = null);
