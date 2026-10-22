using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'job.waiting' payload (setup-once spec §5.6, API §3.5): a running job parked on a named signal, holding no job slot, until 'job.resumed' or its end. Public as it is.</summary>
/// <param name="CommandId">The start command's id.</param>
/// <param name="Job">The job.</param>
/// <param name="Signal">The signal it waits for, for example 'onyx-account'.</param>
/// <param name="Item">The job's item, copied from its start, or null.</param>
/// <param name="Repo">The job's repo, copied from its start (the session filter reads it), or null.</param>
public sealed record JobWaiting(CommandId CommandId, JobId Job, string Signal, ItemId? Item = null, RepoName? Repo = null);
