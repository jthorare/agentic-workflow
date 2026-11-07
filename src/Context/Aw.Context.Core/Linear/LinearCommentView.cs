using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>One comment as Linear has it now.</summary>
/// <param name="Id">The comment's Linear id.</param>
/// <param name="IssueId">The issue's Linear id.</param>
/// <param name="Body">The text as markdown.</param>
public sealed record LinearCommentView(ExternalId Id, ExternalId IssueId, string Body);
