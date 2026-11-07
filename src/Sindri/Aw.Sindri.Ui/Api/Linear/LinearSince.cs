namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>GET /sindri/linear/since (Linear hub spec §3): what changed since a marker, for the strip above the filter bar. Owner only.</summary>
/// <param name="Marker">Pass back as after next time; the app stores it only on Dismiss.</param>
/// <param name="Count">How many changes.</param>
/// <param name="Items">The changes, oldest first.</param>
internal sealed record LinearSince(string Marker, int Count, IReadOnlyList<LinearSinceItem> Items);
