namespace Aw.Context.Infrastructure.Linear;

/// <summary>A comment or attachment a mutation answered with, reduced to the issue it hangs on.</summary>
/// <param name="Issue">The Issue Linear serves.</param>
internal sealed record LinearIssueNode(LinearIssue? Issue = null);
