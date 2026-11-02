using Aw.Context.Core.Extract;

namespace Aw.Context.Infrastructure.Labeling;

/// <summary>What the generate step produced.</summary>
/// <param name="Named">The clusters the model named, each with its candidate labels, best first.</param>
/// <param name="Complete">False when a call failed after some had been answered.</param>
/// <param name="Unavailable">Why no model answered at all, with the fix; null when one did.</param>
/// <param name="Spent">The tokens the calls cost.</param>
internal sealed record Generated(IReadOnlyList<(FileCluster Cluster, List<string> Labels)> Named, bool Complete, string? Unavailable, int Spent);
