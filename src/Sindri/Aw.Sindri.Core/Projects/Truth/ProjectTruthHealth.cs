namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>The health Sindri derives from a project's own PRs and items, beside Linear's.</summary>
/// <param name="Level">On track, at risk, off track or unknown.</param>
/// <param name="Reason">The counts in words: "At risk: 3 of 9 PRs red", "CI not checked yet for 2 of 5 PRs", "At risk: 1 of 4 PRs red, 2 not checked yet". When the oldest red PR's CI time and the page's clock are known, the red count says how long that PR has been red on its current head, from when the failing check completed on GitHub, else from when Sindri first saw that head red: "At risk: 3 of 9 PRs red for 3 d" (min, h or d; #4333). A new push that is still red starts the age again.</param>
public sealed record ProjectTruthHealth(ProjectTruthLevel Level, string Reason);
