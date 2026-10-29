using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ListJobs;

/// <summary>A job as the list shows it: the row without its progress lines and result.</summary>
/// <param name="Id">The job.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Key">Its key.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Service">The service it runs, or null.</param>
/// <param name="State">Where it is.</param>
/// <param name="StartedBy">Who started it.</param>
/// <param name="StartedAt">When.</param>
/// <param name="EndedAt">When it ended, or null.</param>
/// <param name="Rounds">Progress lines recorded.</param>
/// <param name="Summary">How it ended, or null.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
/// <param name="Models">The job's model calls added up, or null when it made none.</param>
/// <param name="WaitingOn">The signal a waiting job is parked on, or null.</param>
/// <param name="WaitingLine">The job's last progress line while it waits (why, and how to resume), or null.</param>
public sealed record JobSummary(JobId Id, JobKind Kind, JobKey Key, ItemId? Item, RepoName? Repo, ServiceName? Service, JobState State, string StartedBy,
    DateTimeOffset StartedAt, DateTimeOffset? EndedAt, int Rounds, string? Summary, string? Code,
    ModelTotals? Models = null, string? WaitingOn = null, string? WaitingLine = null);
