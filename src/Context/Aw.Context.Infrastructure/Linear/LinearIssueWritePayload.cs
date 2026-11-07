namespace Aw.Context.Infrastructure.Linear;

/// <summary>What issueCreate and issueUpdate answer.</summary>
/// <param name="Success">The Success Linear serves; false is a refusal even in a 200.</param>
/// <param name="Issue">The Issue Linear serves.</param>
internal sealed record LinearIssueWritePayload(bool Success = false, LinearIssue? Issue = null);
