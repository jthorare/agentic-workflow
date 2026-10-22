using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>The approved and live profile (API spec §3.1).</summary>
/// <param name="Approved">The approved hash, or null.</param>
/// <param name="ApprovedAt">When it was approved.</param>
/// <param name="Live">The live profile's hash at the last check, or null.</param>
/// <param name="CheckedAt">When it was last checked.</param>
/// <param name="Changed">True when the live profile differs from the approved one.</param>
/// <param name="ChangedPaths">The key paths that differ.</param>
/// <param name="AsOf">How fresh the answer is.</param>
/// <param name="Packs">The lens packs the profile resolves (line spec LN-API8), one per kind and id.</param>
public sealed record GetProfileResponse(ProfileHash? Approved, DateTimeOffset? ApprovedAt, ProfileHash? Live, DateTimeOffset? CheckedAt,
    bool Changed, IReadOnlyList<string> ChangedPaths, AsOf AsOf, IReadOnlyList<ProfilePack> Packs);
