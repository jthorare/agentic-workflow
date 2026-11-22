namespace Aw.Transcripts.Core.Turns;

/// <summary>What orders transcript files for dedupe.</summary>
/// <param name="First">The first timestamp in the file, or null when it has none.</param>
/// <param name="Last">The last timestamp in the file, or null when it has none.</param>
/// <param name="Identity">The file's full path, the last tie-break.</param>
public sealed record FileSpan(DateTimeOffset? First, DateTimeOffset? Last, string Identity);
