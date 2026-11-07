namespace Aw.Sindri.Host.Mcp;

/// <summary>One free-text field a tool result fences.</summary>
/// <param name="Path">Where the field is: camelCase names joined by '.', a name ending in "[]" for every element of an array (groups[].items[].title; a path may end in "[]" for an array of strings).</param>
/// <param name="AuthorField">The field next to it that names who wrote the text, or null when the row has none.</param>
/// <param name="Mark">Whether the object holding the field gains the trust member; false leaves it to a field of the same row that does (a nested name inside a row that is already marked).</param>
/// <param name="EveryString">Whether the field holds a value of any shape (a job's result) whose string values, however deep, are all fenced; numbers, booleans and nulls stay as they are.</param>
// why: the Linear list fences nested names inside a row that its title already marks, and the agreed golden carries trust on the row and the viewer only
internal sealed record FenceField(string Path, string? AuthorField = null, bool EveryString = false, bool Mark = true);
