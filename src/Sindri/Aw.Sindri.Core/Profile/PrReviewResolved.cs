namespace Aw.Sindri.Core.Profile;

/// <summary>One settings entry with every key filled in, and the name of the entry each key came from (the 'sindri review settings' view).</summary>
/// <param name="Name">The entry's name ('default' for the built-in).</param>
/// <param name="Panels">The panels, in order.</param>
/// <param name="Groups">The file groups, first match wins.</param>
/// <param name="Collapse">The folded globs.</param>
/// <param name="Risky">The risky paths.</param>
/// <param name="Checks">The required checks.</param>
/// <param name="Sources">For each of panels, groups, collapse, risky, checks: the entry the value came from.</param>
public sealed record PrReviewResolved(
    string Name,
    IReadOnlyList<string> Panels,
    IReadOnlyList<PrReviewGroup> Groups,
    IReadOnlyList<string> Collapse,
    IReadOnlyList<PrReviewRisky> Risky,
    IReadOnlyList<PrReviewCheck> Checks,
    IReadOnlyDictionary<string, string> Sources);
