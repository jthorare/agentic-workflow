namespace Aw.Sindri.Core.Events;

/// <summary>The peaks of every limit over a session, so the limits are tuned from data (sandbox spec §8, owner decision Q5).</summary>
/// <param name="MemoryBytes">The highest memory use.</param>
/// <param name="CpuPercent">The highest CPU use, in percent of one CPU.</param>
/// <param name="Tasks">The most processes.</param>
/// <param name="WallSeconds">The unsuspended seconds the session ran.</param>
public sealed record LimitPeaks(long MemoryBytes, int CpuPercent, int Tasks, long WallSeconds);
