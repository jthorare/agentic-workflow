using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The profile's approval state.</summary>
/// <param name="Approved">The approved hash the process runs with, if any.</param>
/// <param name="Live">The live file's hash, when it loads.</param>
/// <param name="ChangedSinceApproval">True when the live file differs from the saved approval.</param>
/// <param name="Problem">Why the live profile file doesn't load, if it doesn't (an unreadable approval or ledger is the answer's error instead).</param>
/// <param name="Source">The live profile file, when it loads.</param>
/// <param name="NeedsApproval">
/// True when the live profile isn't approved by <see cref="Core.Profile.ProfileRules.IsApproved"/>: nothing is saved, the
/// live file changed since the saved approval, or the saved approval isn't the latest recorded 'profile.approved' event
/// (missing from the ledger, or followed by a later one). A live file that doesn't load is judged by the saved approval.
/// </param>
public sealed record ProfileStatus(ProfileHash? Approved, ProfileHash? Live, bool ChangedSinceApproval, string? Problem, string? Source, bool NeedsApproval);
