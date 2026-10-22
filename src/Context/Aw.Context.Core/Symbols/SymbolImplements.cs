using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Symbols;

/// <summary>A symbol that implements a map node (Phase 9): the file-level 'implements' link refined to the symbol of that file whose name says so. Read from the code index at the map's commit, never stored in the map.</summary>
/// <param name="Node">The node it implements.</param>
/// <param name="Ref">The symbol's reference ('path#name').</param>
/// <param name="Name">Its name.</param>
/// <param name="Path">The file that defines it.</param>
/// <param name="StartLine">Its first line.</param>
/// <param name="Confidence">How sure the match is: 0.9, a name match; the file-level link's own confidence is unchanged.</param>
public sealed record SymbolImplements(NodeKey Node, string Ref, string Name, string Path, int StartLine, double Confidence);
