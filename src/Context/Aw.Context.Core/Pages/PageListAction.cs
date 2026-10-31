namespace Aw.Context.Core.Pages;

/// <summary>One action line of a pasted block: 'Name' or 'Name -> concept, concept'.</summary>
/// <param name="Name">The action name.</param>
/// <param name="Concepts">The concept names written after '->', trimmed, blanks dropped; null when the line has no '->' (a matched action then keeps the map's concepts), empty when the arrow has nothing after it.</param>
public sealed record PageListAction(string Name, IReadOnlyList<string>? Concepts);
