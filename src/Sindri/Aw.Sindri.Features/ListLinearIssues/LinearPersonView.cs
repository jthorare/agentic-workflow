namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>A Linear user as the list names one: the viewer or an assignee. The name is Linear text: fenced for a session.</summary>
/// <param name="Id">The user's Linear id.</param>
/// <param name="Name">The display name.</param>
internal sealed record LinearPersonView(string Id, string Name);
