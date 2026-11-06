namespace Aw.Context.Testing.Linear;

/// <summary>An attachment of the stateful fake Linear, read-only.</summary>
/// <param name="Id">The attachment id.</param>
/// <param name="IssueId">The issue it is on.</param>
/// <param name="Url">The linked URL.</param>
/// <param name="Title">The title.</param>
/// <param name="CreatedAt">When it was created.</param>
public sealed record FakeLinearAttachment(string Id, string IssueId, string Url, string Title, string CreatedAt);
