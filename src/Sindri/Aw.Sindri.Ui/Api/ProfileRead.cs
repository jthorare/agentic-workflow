namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/profile (api #782): the plain-words summary of what approving would let Sindri do, and the hashes and key paths the page sheet checks before it approves. The rest of the answer is not read.</summary>
/// <param name="Summary">Null when there is nothing to approve.</param>
/// <param name="Approved">The approved profile's hash, or null when none is approved.</param>
/// <param name="Live">The live profile's hash at the last check, or null.</param>
/// <param name="ChangedPaths">The key paths that differ from the approved profile; null from a server before the field.</param>
internal sealed record ProfileRead(ProfileSummaryView? Summary, string? Approved = null, string? Live = null, IReadOnlyList<string>? ChangedPaths = null);
