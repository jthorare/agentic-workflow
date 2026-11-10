namespace Aw.Sindri.Core.Resources;

/// <summary>The most a box lets one sandbox have: what a declared sandbox size is checked against when a workflow is activated.</summary>
/// <param name="MemoryBytes">The memory cap in bytes.</param>
/// <param name="Wall">The wall-clock cap.</param>
public sealed record ResourceBoxCap(long MemoryBytes, TimeSpan Wall);
