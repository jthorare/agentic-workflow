namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One choice of a filter.</summary>
/// <param name="Value">What the query carries (a team key, an id, a priority number); null leaves the filter off.</param>
/// <param name="Text">The words.</param>
internal sealed record LinearFilterChoice(string? Value, string Text);
