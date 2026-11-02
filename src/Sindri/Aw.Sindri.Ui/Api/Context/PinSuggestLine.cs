namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One line of a pasted pin list.</summary>
/// <param name="Name">The name to pin.</param>
/// <param name="Definition">The words that define it, clipped so name + definition stay at 80 characters, or null.</param>
internal sealed record PinSuggestLine(string Name, string? Definition = null);
