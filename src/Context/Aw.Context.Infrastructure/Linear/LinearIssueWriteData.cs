namespace Aw.Context.Infrastructure.Linear;

/// <summary>The data of the IssueCreate and IssueUpdate operations: the one that ran has its payload, the other is absent.</summary>
/// <param name="IssueCreate">The IssueCreate Linear serves.</param>
/// <param name="IssueUpdate">The IssueUpdate Linear serves.</param>
internal sealed record LinearIssueWriteData(LinearIssueWritePayload? IssueCreate = null, LinearIssueWritePayload? IssueUpdate = null);
