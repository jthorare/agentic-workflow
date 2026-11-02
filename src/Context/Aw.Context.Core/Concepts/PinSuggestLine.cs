namespace Aw.Context.Core.Concepts;

/// <summary>One line of a pasted pin list: the name to pin and the words that define it.</summary>
/// <param name="Name">The name on the line (at most 80 characters with the definition).</param>
/// <param name="Definition">The words that define it, or null when the line gave none.</param>
public sealed record PinSuggestLine(string Name, string? Definition);
