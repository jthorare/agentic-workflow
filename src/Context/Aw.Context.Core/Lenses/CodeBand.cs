namespace Aw.Context.Core.Lenses;

/// <summary>The code band: one entry per file that implements or tests a scope node.</summary>
/// <param name="Files">The files, at most 100.</param>
/// <param name="Symbols">Always unavailable until the code index exists (Phase 9).</param>
/// <param name="Counts">The full counts.</param>
/// <param name="Truncated">Always false: a band lists every item (kept so the wire shape stays).</param>
public sealed record CodeBand(IReadOnlyList<LensFile> Files, LensSymbols Symbols, CodeCounts Counts, bool Truncated);
