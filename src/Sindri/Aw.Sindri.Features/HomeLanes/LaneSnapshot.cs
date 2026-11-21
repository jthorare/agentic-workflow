namespace Aw.Sindri.Features.HomeLanes;

/// <summary>What the three read-only aw-meta commands said.</summary>
/// <param name="Lanes">The lanes and their context.</param>
/// <param name="Queue">The merge queue.</param>
/// <param name="Usage">The usage subjects.</param>
internal sealed record LaneSnapshot(IReadOnlyList<LaneContext> Lanes, IReadOnlyList<QueuedPr> Queue, IReadOnlyList<UsageSubject> Usage);
