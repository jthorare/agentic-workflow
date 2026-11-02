namespace Aw.Context.Core.Surfaces;

/// <summary>How a pass's groups were settled (spec §7.1 'how'): merged + kept + rejected is the group count, and undecided is the part of kept that Jev did not settle.</summary>
/// <param name="Merged">Groups merged.</param>
/// <param name="Kept">Groups kept separate, chosen or undecided.</param>
/// <param name="Rejected">Groups rejected.</param>
/// <param name="Undecided">The part of <paramref name="Kept"/> that Jev did not settle.</param>
public sealed record SurfaceCounts(int Merged, int Kept, int Rejected, int Undecided);
