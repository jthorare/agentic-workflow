namespace Aw.Context.Infrastructure.Linear;

/// <summary>What commentCreate and commentUpdate answer.</summary>
/// <param name="Success">The Success Linear serves; false is a refusal even in a 200.</param>
/// <param name="Comment">The Comment Linear serves.</param>
internal sealed record LinearCommentWritePayload(bool Success = false, LinearIssueNode? Comment = null);
