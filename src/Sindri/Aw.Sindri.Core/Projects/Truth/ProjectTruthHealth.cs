namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>The health Sindri derives from a project's own PRs and items, beside Linear's.</summary>
/// <param name="Level">On track, at risk, off track or unknown.</param>
/// <param name="Reason">The counts in words: "At risk: 3 of 9 PRs red", "CI unknown for 2 of 5 PRs", "At risk: 1 of 4 PRs red, 2 unknown".</param>
public sealed record ProjectTruthHealth(ProjectTruthLevel Level, string Reason);
