namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/profile (api #782), the part the approval shows: the plain-words summary of what approving would let Sindri do. The rest of the answer is not read.</summary>
/// <param name="Summary">Null when there is nothing to approve.</param>
/// <param name="Approved">The approved profile's hash, or null when none is approved.</param>
internal sealed record ProfileRead(ProfileSummaryView? Summary, string? Approved = null);
