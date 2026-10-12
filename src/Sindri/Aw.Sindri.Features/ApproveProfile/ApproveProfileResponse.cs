using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>The live hash, the key paths that differ from the latest approval, and whether it is now approved.</summary>
/// <param name="Hash">The live profile's hash.</param>
/// <param name="Changes">Key paths changed since the latest approval (every path when there was none).</param>
/// <param name="Approved">True when this hash is now (or already was) the approval.</param>
public sealed record ApproveProfileResponse(ProfileHash Hash, IReadOnlyList<string> Changes, bool Approved);
