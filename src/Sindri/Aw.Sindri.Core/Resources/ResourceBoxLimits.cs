namespace Aw.Sindri.Core.Resources;

/// <summary>What the box and the profile allow one session; a declared sandbox size is clamped to it.</summary>
/// <param name="MemoryBytes">Memory per session.</param>
/// <param name="CpuPercent">CPU share per session.</param>
/// <param name="Wall">The longest wall-clock limit.</param>
public sealed record ResourceBoxLimits(long MemoryBytes, int CpuPercent, TimeSpan Wall);
