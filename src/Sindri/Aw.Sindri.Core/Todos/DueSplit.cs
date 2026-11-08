namespace Aw.Sindri.Core.Todos;

/// <summary>Typed todo text with its trailing due word taken off (todo spec D7).</summary>
/// <param name="Text">The text without the due word.</param>
/// <param name="Due">The date the due word read as, or null when there was none.</param>
public sealed record DueSplit(string Text, DateOnly? Due);
