using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'repo.added' payload as the ledger keeps it (setup-once spec §7.5): the folder is private, so the public form leaves it out. Key 'repo.added:&lt;repo&gt;': one per repo name, so a backfill and the add job cannot both record it.</summary>
/// <param name="CommandId">The 'AddRepo' command's id.</param>
/// <param name="Repo">The repo's name in the profile.</param>
/// <param name="Path">The absolute path of its checkout.</param>
public sealed record RepoAdded(CommandId CommandId, RepoName Repo, string Path);
