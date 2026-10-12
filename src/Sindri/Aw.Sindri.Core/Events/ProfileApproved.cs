using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'profile.approved' payload.</summary>
/// <param name="Hash">The approved hash.</param>
/// <param name="Changes">The key paths that changed since the previous approval.</param>
public sealed record ProfileApproved(ProfileHash Hash, IReadOnlyList<string> Changes);
