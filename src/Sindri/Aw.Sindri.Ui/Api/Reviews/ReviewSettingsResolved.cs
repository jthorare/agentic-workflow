namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The settings a PR gets, every key filled in, with where each came from.</summary>
/// <param name="Panels">The shown panels in order.</param>
/// <param name="Groups">The diff groups; empty while the default's "by the first two path segments" applies.</param>
/// <param name="Collapse">The folded globs.</param>
/// <param name="Risky">The risky paths.</param>
/// <param name="Checks">The required checks.</param>
/// <param name="From">Per key (panels, groups, collapse, risky, checks): the entry it came from, or "default".</param>
internal sealed record ReviewSettingsResolved(
    IReadOnlyList<string> Panels,
    IReadOnlyList<ReviewSettingsGroup> Groups,
    IReadOnlyList<string> Collapse,
    IReadOnlyList<ReviewSettingsRisk> Risky,
    IReadOnlyList<ReviewSettingsCheck> Checks,
    IReadOnlyDictionary<string, string> From);
