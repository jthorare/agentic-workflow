namespace Aw.Sindri.Core.Events;

/// <summary>The 'memory.facts.forgotten' payload (#2460): facts were deleted from a memory. The envelope's actor is who forgot them. Counts and the selector's kind only, never fact text.</summary>
/// <param name="Memory">The memory's name.</param>
/// <param name="Selector">How the facts were chosen: 'fact', 'tag' or 'expired'.</param>
/// <param name="Count">How many facts were deleted.</param>
/// <param name="At">When they were deleted.</param>
public sealed record MemoryFactsForgotten(string Memory, string Selector, int Count, DateTimeOffset At);
