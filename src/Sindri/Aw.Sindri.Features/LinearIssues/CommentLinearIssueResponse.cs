using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>A comment that is in Linear.</summary>
/// <param name="Outcome">Always 'applied': an unknown outcome is the error SND-LINEAR-011.</param>
/// <param name="ClientId">The id the app sent.</param>
/// <param name="Comment">The comment as the issue now holds it.</param>
/// <param name="Slow">True when the comment landed only on the one re-read after Linear gave no answer.</param>
/// <param name="Warning">SND-LINEAR-013 when Linear took the comment and Sindri's store did not (the next sync stores it); left out otherwise.</param>
internal sealed record CommentLinearIssueResponse(
    string Outcome,
    string ClientId,
    LinearCommentView Comment,
    bool Slow,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Warning = null);
