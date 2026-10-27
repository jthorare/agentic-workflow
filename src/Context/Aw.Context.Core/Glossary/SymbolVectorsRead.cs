namespace Aw.Context.Core.Glossary;

/// <summary>What <see cref="ISymbolVectors"/> found for some files.</summary>
/// <param name="Vectors">The embedded symbols, each with its file.</param>
/// <param name="Missing">The asked paths that have no vector.</param>
public sealed record SymbolVectorsRead(IReadOnlyList<PathVector> Vectors, IReadOnlyList<string> Missing);
