namespace Aw.Sindri.Features.LinearIssues;

/// <summary>An issue's label. The name is Linear text: fenced for a session.</summary>
/// <param name="Id">The label's Linear id.</param>
/// <param name="Name">The label's name.</param>
/// <param name="Color">The label's color as Linear spells it ('#eb5757'); null when none is stored.</param>
internal sealed record LinearLabelView(string Id, string Name, string? Color);
