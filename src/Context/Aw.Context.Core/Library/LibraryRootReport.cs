namespace Aw.Context.Core.Library;

/// <summary>What the library feed's last pass over one root found; the feed writes it, <see cref="LibraryRootStatus"/> is read from it.</summary>
/// <param name="Root">'aw' or the vault's name.</param>
/// <param name="Path">The root's folder.</param>
/// <param name="Problem">Why the root could not be listed, or null.</param>
/// <param name="Docs">The design docs found.</param>
/// <param name="NearMisses">The near misses, each with its root-relative path and its SND-LIB-019 error (line, message and fix).</param>
/// <param name="LastRead">When the pass finished.</param>
public sealed record LibraryRootReport(string Root, string Path, string? Problem, int Docs, IReadOnlyList<DesignDocFinding> NearMisses, DateTimeOffset LastRead);
