using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/linear/issues/{key}/comments: the app's client id and the comment; the issue comes from the route.</summary>
/// <param name="ClientId">The app's uuid, made at the box's first character.</param>
/// <param name="Body">The comment as markdown.</param>
internal sealed record CommentLinearIssueBody(string ClientId, string Body)
{
    /// <summary>The request for <paramref name="key"/>.</summary>
    /// <param name="key">The route's issue key.</param>
    /// <returns>The request.</returns>
    internal CommentLinearIssueRequest ToRequest(string key) => new(key, ClientId, Body);
}
