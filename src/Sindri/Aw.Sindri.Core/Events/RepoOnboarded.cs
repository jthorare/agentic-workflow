using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'repo.onboarded' payload (setup-once spec §7.5): the approved profile names the repo and its onboarding ran. Key 'repo.onboarded:&lt;repo&gt;:&lt;hash&gt;'.</summary>
/// <param name="CommandId">The id of the service run that recorded it.</param>
/// <param name="Repo">The repo.</param>
/// <param name="ApprovedHash">The approved profile that names it.</param>
public sealed record RepoOnboarded(CommandId CommandId, RepoName Repo, ProfileHash ApprovedHash);
