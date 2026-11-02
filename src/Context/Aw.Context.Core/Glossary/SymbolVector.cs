namespace Aw.Context.Core.Glossary;

/// <summary>One code symbol's embedding: its display name (a plain string, not an id) and its vector.</summary>
/// <param name="Name">The symbol's name as written in code, e.g. 'SplitPaneLayout' or 'split_pane'.</param>
/// <param name="Vector">The embedding; vectors of a different length than the feature's first are ignored.</param>
public sealed record SymbolVector(string Name, ReadOnlyMemory<float> Vector);
