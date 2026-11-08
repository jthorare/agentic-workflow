using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>Post a comment on a Linear issue (POST /sindri/linear/issues/{key}/comments; Linear hub spec 2026-11-04 §3, §5.3): desk class, the owner's, an agent session is refused (SND-AUTH-002). Writes to Linear through Context; nothing is appended to the ledger.</summary>
/// <param name="Key">The issue's key, 'ENG-12'.</param>
/// <param name="ClientId">The app's uuid, made at the box's first character; a retry sends the same one, so the comment is never posted twice.</param>
/// <param name="Body">The comment as markdown, 1 to 65,536 characters.</param>
internal sealed record CommentLinearIssueRequest(string Key, string ClientId, string Body) : ICommand<CommentLinearIssueResponse>;
