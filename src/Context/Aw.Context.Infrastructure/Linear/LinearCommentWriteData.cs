namespace Aw.Context.Infrastructure.Linear;

/// <summary>The data of the CommentCreate and CommentUpdate operations: the one that ran has its payload, the other is absent.</summary>
/// <param name="CommentCreate">The CommentCreate Linear serves.</param>
/// <param name="CommentUpdate">The CommentUpdate Linear serves.</param>
internal sealed record LinearCommentWriteData(LinearCommentWritePayload? CommentCreate = null, LinearCommentWritePayload? CommentUpdate = null);
