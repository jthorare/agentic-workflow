using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'job.resumed' payload (setup-once spec §5.6, API §3.5): the signal a waiting job was parked on came, and it runs again. Public as it is.</summary>
/// <param name="CommandId">The start command's id.</param>
/// <param name="Job">The job.</param>
/// <param name="Signal">The signal that came.</param>
/// <param name="Item">The job's item, copied from its start, or null.</param>
/// <param name="Repo">The job's repo, copied from its start, or null.</param>
public sealed record JobResumed(CommandId CommandId, JobId Job, string Signal, ItemId? Item = null, RepoName? Repo = null);
