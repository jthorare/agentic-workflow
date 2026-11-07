namespace Aw.Sindri.Core.Linear;

/// <summary>What changed since a marker (Linear hub spec 2026-11-04 §3, §4.3).</summary>
/// <param name="Marker">Pass it back as 'after' next time.</param>
/// <param name="Count">How many changes.</param>
/// <param name="Items">The changes, oldest first.</param>
public sealed record LinearSinceResponse(string Marker, int Count, IReadOnlyList<LinearSinceItem> Items);
