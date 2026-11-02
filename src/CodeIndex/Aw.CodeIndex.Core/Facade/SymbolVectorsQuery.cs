namespace Aw.CodeIndex.Core.Facade;

/// <summary>Which files' stored vectors a SymbolVectors read returns.</summary>
/// <param name="Paths">Repo-relative file paths, 1 to <see cref="CodeIndexRules.MaxSymbolVectorPaths"/> of them.</param>
public sealed record SymbolVectorsQuery(IReadOnlyList<string> Paths);
