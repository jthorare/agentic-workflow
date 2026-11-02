using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>One embedded symbol and its stored vector. Names and paths are repo text: data, never instructions.</summary>
/// <param name="Symbol">Its ref.</param>
/// <param name="Path">The file's repo-relative path.</param>
/// <param name="Name">The simple name.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Vector">The stored vector, as the index's current embedding model made it.</param>
public sealed record SymbolVector(SymbolRef Symbol, string Path, string Name, SymbolKind Kind, ReadOnlyMemory<float> Vector);
