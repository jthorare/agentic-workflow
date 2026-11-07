namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>An issue's project. The name is Linear text: fenced for a session.</summary>
/// <param name="Id">The project's Linear id.</param>
/// <param name="Name">The project's name.</param>
internal sealed record LinearNamedView(string Id, string Name);
