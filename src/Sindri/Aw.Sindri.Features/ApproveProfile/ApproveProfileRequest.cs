using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>
/// Approve the live profile (design spec §8.7, §11.1). Without a hash it only shows the hash and what changed. With one,
/// the hash must be the live profile's and <see cref="Confirmed"/> must be set: the CLI sets it only after the human typed
/// the hash's first six characters at an interactive terminal.
/// </summary>
/// <param name="Hash">The hash to approve, or null to see the live hash and the changes.</param>
/// <param name="Confirmed">True only after the human's typed confirmation.</param>
public sealed record ApproveProfileRequest(ProfileHash? Hash, bool Confirmed);
