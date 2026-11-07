using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;

namespace Aw.Context.Testing.Linear;

/// <summary>One call <see cref="FakeContextLinearWrites"/> would have sent to Linear: the method and the arguments that method takes, the rest null.</summary>
/// <param name="Method">The facade method: Live, Create, Change, Comment, EditComment or Attach.</param>
/// <param name="Key">The issue's key (every method but Create).</param>
/// <param name="ClientId">The client id (Create and Comment).</param>
/// <param name="Draft">The new issue (Create).</param>
/// <param name="Change">The fields sent (Change).</param>
/// <param name="Body">The text (Comment and EditComment).</param>
/// <param name="CommentId">The comment's id (EditComment).</param>
/// <param name="Url">The link (Attach).</param>
/// <param name="Title">The attachment's title (Attach).</param>
public sealed record FakeLinearWriteCall(
    string Method,
    LinearIssueKey? Key = null,
    LinearClientId? ClientId = null,
    LinearIssueDraft? Draft = null,
    LinearIssueChange? Change = null,
    string? Body = null,
    ExternalId? CommentId = null,
    Uri? Url = null,
    string? Title = null);
