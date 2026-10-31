namespace Aw.Context.Core.Pages;

/// <summary>One action line of a pasted block: 'Name' or 'Name -> concept, concept'.</summary>
/// <param name="Name">The action name.</param>
/// <param name="Concepts">The concept names written after '->', trimmed, blanks dropped; empty when none were written.</param>
public sealed record PageListAction(string Name, IReadOnlyList<string> Concepts);
