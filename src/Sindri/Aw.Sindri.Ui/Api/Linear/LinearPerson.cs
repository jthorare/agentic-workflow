namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>A Linear user as the answers name one: the assignee, a comment author, an actor or the viewer. The session form's trust flag is not read.</summary>
/// <param name="Id">The user's Linear id.</param>
/// <param name="Name">The display name.</param>
internal sealed record LinearPerson(string Id, string Name);
