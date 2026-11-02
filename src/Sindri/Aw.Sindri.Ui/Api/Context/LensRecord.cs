namespace Aw.Sindri.Ui.Api.Context;

/// <summary>An intent record in the intent band.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Source">Source, as served.</param>
/// <param name="Mentions">Mentions, as served.</param>
/// <param name="Trust">Trust, as served.</param>
/// <param name="State">State, as served.</param>
/// <param name="Cites">The markers of its citations in the answer's map; none when it is uncited.</param>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Anchors">Anchors, as served.</param>
/// <param name="Depth">Depth, as served.</param>
/// <param name="Repo">The repo it belongs to, or null (left out of the answer) when it belongs to no single repo.</param>
internal sealed record LensRecord(
    string Key,
    string Label,
    string Source,
    IReadOnlyList<string> Mentions,
    string Trust,
    string State,
    IReadOnlyList<string> Cites,
    string Kind,
    IReadOnlyList<string>? Anchors = null,
    int? Depth = null,
    string? Repo = null);
