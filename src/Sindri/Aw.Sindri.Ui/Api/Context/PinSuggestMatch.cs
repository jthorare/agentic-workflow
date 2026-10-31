namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The concept one line of a pasted list matches.</summary>
/// <param name="Line">The line's 0-based index in the whole list.</param>
/// <param name="Concept">The matched concept's key, or null when none fits.</param>
/// <param name="Name">The concept's name, or null.</param>
internal sealed record PinSuggestMatch(int Line, string? Concept, string? Name);
