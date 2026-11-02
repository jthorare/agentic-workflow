using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>The approved and live profile (API spec §3.1).</summary>
/// <param name="Approved">The approved hash, or null.</param>
/// <param name="ApprovedAt">When it was approved.</param>
/// <param name="Live">The live profile's hash at the last check, or null.</param>
/// <param name="FileHash">The hash of the live profile file as it is on disk when asked (what a write's 'ifHash' is compared with), or null when the file is missing or doesn't parse. Unlike <paramref name="Live"/> it doesn't wait for a check.</param>
/// <param name="CheckedAt">When it was last checked.</param>
/// <param name="Changed">True when the live profile differs from the approved one.</param>
/// <param name="ChangedPaths">The key paths that differ.</param>
/// <param name="AsOf">How fresh the answer is.</param>
/// <param name="Packs">The lens packs the profile resolves (line spec LN-API8), one per kind and id.</param>
/// <param name="Summary">What approving the live profile would let Sindri do, in plain words; null when the profile is approved and unchanged, or the live one asks for nothing new.</param>
public sealed record GetProfileResponse(ProfileHash? Approved, DateTimeOffset? ApprovedAt, ProfileHash? Live, ProfileHash? FileHash, DateTimeOffset? CheckedAt,
    bool Changed, IReadOnlyList<string> ChangedPaths, AsOf AsOf, IReadOnlyList<ProfilePack> Packs, ProfileSummary? Summary = null);
