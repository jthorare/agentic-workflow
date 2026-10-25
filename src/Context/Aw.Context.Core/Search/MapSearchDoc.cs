namespace Aw.Context.Core.Search;

/// <summary>What the index holds of one node: its cleaned words in three fields, and its area. The text is cleaned already, never fenced.</summary>
/// <param name="Id">The document's id: its place in the list the index was built from.</param>
/// <param name="Title">The label.</param>
/// <param name="Path">The file paths the node stands for or rests on, space separated.</param>
/// <param name="Text">Everything else it says (a decision's quote, descriptive attributes).</param>
/// <param name="Area">The area the node belongs to.</param>
public sealed record MapSearchDoc(int Id, string Title, string Path, string Text, string Area);
