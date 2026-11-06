namespace Aw.Context.Testing.Linear;

/// <summary>A comment of the stateful fake Linear, read-only.</summary>
/// <param name="Id">The comment id (the client's, when commentCreate sent one).</param>
/// <param name="IssueId">The issue it is on.</param>
/// <param name="Body">The body.</param>
/// <param name="User">The author's name.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record FakeLinearComment(string Id, string IssueId, string Body, string User, string CreatedAt, string UpdatedAt);
