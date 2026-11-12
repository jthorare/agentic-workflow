using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>One running job as `sindri status` lists it (issue #2945): a code index build or embed pass runs as a job, not as a work item.</summary>
/// <param name="Id">The job.</param>
/// <param name="Kind">What it does ('BuildCodeIndex', 'EmbedCodeIndex', ...).</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Service">The service that started it, or null when no service did.</param>
/// <param name="Progress">Its last progress line, or null before the first.</param>
/// <param name="StartedAt">When it started.</param>
public sealed record RunningJobStatus(JobId Id, JobKind Kind, RepoName? Repo, string? Service, string? Progress, DateTimeOffset StartedAt);
