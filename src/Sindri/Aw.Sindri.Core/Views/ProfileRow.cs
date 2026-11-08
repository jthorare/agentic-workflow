using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Views;

/// <summary>The 'profile' view's one row.</summary>
/// <param name="Approved">The approved hash, or null.</param>
/// <param name="ApprovedAt">When it was approved.</param>
/// <param name="Live">The live profile's hash at the last check, or null.</param>
/// <param name="CheckedAt">When it was last checked.</param>
/// <param name="Changed">True when the live profile differs from the approved one.</param>
/// <param name="ChangedPaths">The key paths that differ.</param>
/// <param name="ApprovedServices">The services the approved profile names.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
public sealed record ProfileRow(ProfileHash? Approved, DateTimeOffset? ApprovedAt, ProfileHash? Live, DateTimeOffset? CheckedAt,
    bool Changed, IReadOnlyList<string> ChangedPaths, IReadOnlyList<ServiceName> ApprovedServices, long Sequence);
