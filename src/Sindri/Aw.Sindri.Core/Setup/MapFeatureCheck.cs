namespace Aw.Sindri.Core.Setup;

/// <summary>What the current map says about some feature keys.</summary>
/// <param name="Version">The map read ('v41'), or null when no map is built yet.</param>
/// <param name="Missing">The keys the map has no node for; every key when there is no map.</param>
public sealed record MapFeatureCheck(string? Version, IReadOnlyList<string> Missing);
