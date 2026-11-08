using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>An issue that is in Linear (201).</summary>
/// <param name="Outcome">Always 'applied': an unknown outcome is the error SND-LINEAR-011.</param>
/// <param name="ClientId">The id the app sent.</param>
/// <param name="Issue">The new issue, in the shape of a row of GET /sindri/linear/issues.</param>
/// <param name="Warning">SND-LINEAR-013 when Linear took the issue and Sindri's store did not; left out otherwise.</param>
internal sealed record CreateLinearIssueResponse(
    string Outcome,
    string ClientId,
    LinearIssueRowView Issue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Warning = null);
