namespace Aw.Sindri.Features.LinearIssues;

/// <summary>A comment on an issue. The author's name and the body are Linear text: fenced for a session.</summary>
/// <param name="Id">The comment's Linear id.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="Body">The text.</param>
/// <param name="Sindri">True when Sindri wrote it.</param>
internal sealed record LinearCommentView(string Id, LinearPersonView Author, DateTimeOffset CreatedAt, string Body, bool Sindri);
