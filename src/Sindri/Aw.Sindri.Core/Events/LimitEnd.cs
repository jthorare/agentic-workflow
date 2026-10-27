namespace Aw.Sindri.Core.Events;

/// <summary>What a limit kill adds to 'session.ended' (sandbox spec §6): which limit, its value, the peak and the peaks of every limit. Units as in <see cref="SessionEnded"/>.</summary>
/// <param name="Kind">Memory or wall.</param>
/// <param name="Limit">The limit's value (bytes for memory, seconds for wall).</param>
/// <param name="Peak">The peak of that limit, in the same unit.</param>
/// <param name="Peaks">The peaks of every limit, or null.</param>
public sealed record LimitEnd(LimitKind Kind, long Limit, long Peak, LimitPeaks? Peaks = null);
