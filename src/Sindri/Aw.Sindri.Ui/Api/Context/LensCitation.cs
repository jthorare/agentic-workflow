namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What a lens record's citation says about where it opens outside Sindri (#1702).</summary>
/// <param name="Key">The record's key, as served.</param>
/// <param name="Permalink">Where the record opens at its source, or null when there is no link to give.</param>
/// <param name="Aliases">The record's other names, such as the Linear identifier "SPL-103".</param>
/// <param name="State">Current, stale, unchecked, unstable or scrub-blocked.</param>
internal sealed record LensCitation(
    string Key,
    Uri? Permalink,
    IReadOnlyList<string>? Aliases,
    string State);
