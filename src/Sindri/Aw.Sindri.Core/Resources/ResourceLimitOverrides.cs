namespace Aw.Sindri.Core.Resources;

/// <summary>Per-run sandbox limits, already clamped to the box. Null means "use the profile's".</summary>
/// <param name="MemoryBytes">Memory.</param>
/// <param name="CpuPercent">CPU share.</param>
/// <param name="Wall">Wall-clock limit.</param>
public sealed record ResourceLimitOverrides(long? MemoryBytes, int? CpuPercent, TimeSpan? Wall);
