namespace Aw.Meta.Core.Ports;

/// <summary>The latest move of main.</summary>
/// <param name="MainSha">Main's sha after the move.</param>
/// <param name="MergedPrs">The pull requests that merged with it.</param>
/// <param name="At">When main moved.</param>
/// <param name="Version">The board version: 1 for the first recorded move, up by one for each later one.</param>
public sealed record BoardFacts(string MainSha, IReadOnlyList<int> MergedPrs, DateTimeOffset At, int Version);
