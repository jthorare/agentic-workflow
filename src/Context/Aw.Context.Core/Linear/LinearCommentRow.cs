using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>A comment as the 'linear_comment' side table keeps it (spec 2026-11-04 §4.1).</summary>
/// <param name="Id">Linear's id of the comment.</param>
/// <param name="AuthorId">The author's Linear user id; null when Linear didn't say (a deleted user, an integration).</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="Body">Its text, as Linear has it.</param>
/// <param name="Sindri">True when the body carries Sindri's marker line and the author is the write key's user.</param>
public sealed record LinearCommentRow(ExternalId Id, ExternalId? AuthorId, DateTimeOffset CreatedAt, string Body, bool Sindri);
