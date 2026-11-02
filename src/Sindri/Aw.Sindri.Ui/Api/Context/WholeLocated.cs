namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Which area a located node is in.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Area">Its area; null when the key is not in the map.</param>
/// <param name="Concept">The ids of the concepts it belongs to ("concept:other" when none claims it); null when the server does not say.</param>
internal sealed record WholeLocated(string Key, string? Area, IReadOnlyList<string>? Concept = null);
