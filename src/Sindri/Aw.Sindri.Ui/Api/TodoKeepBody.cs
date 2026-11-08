namespace Aw.Sindri.Ui.Api;

/// <summary>The body of POST /sindri/todos/{id}/keep (todo spec §4.4): which version wins.</summary>
/// <param name="Side">"sindri" or "obsidian".</param>
internal sealed record TodoKeepBody(string Side);
