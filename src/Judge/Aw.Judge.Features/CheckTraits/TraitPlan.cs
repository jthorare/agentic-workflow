namespace Aw.Judge.Features.CheckTraits;

/// <summary>What a check will ask: the repo's root and one task per excerpt and group of traits.</summary>
/// <param name="Root">The repo's top-level directory.</param>
/// <param name="Tasks">The Jev requests.</param>
internal sealed record TraitPlan(string Root, IReadOnlyList<TraitTask> Tasks);
