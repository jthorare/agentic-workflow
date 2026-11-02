using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>The concept one line of a pasted list matches.</summary>
/// <param name="Line">The 0-based index of the line in the query.</param>
/// <param name="Concept">The matched concept, or null when no concept fits.</param>
/// <param name="Name">The concept label, or null.</param>
public sealed record PinSuggestMatch(int Line, NodeKey? Concept, string? Name);
