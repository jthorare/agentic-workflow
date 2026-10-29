using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>A row of the 'jobs' view (keyed by job id): a job folded from its events.</summary>
/// <param name="Id">The job.</param>
/// <param name="Kind">The catalog action that started it.</param>
/// <param name="Key">Its idempotency key within the kind.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Service">The service it runs ('RunService'), or null.</param>
/// <param name="State">Where it is.</param>
/// <param name="StartedBy">The actor that started it.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="EndedAt">When it ended, or null while running.</param>
/// <param name="Progress">The last progress lines.</param>
/// <param name="Rounds">The highest progress round seen.</param>
/// <param name="Summary">How it ended, in a line, or null while running.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
/// <param name="Result">The work's result JSON when it succeeded and kept one, otherwise null.</param>
/// <param name="CancelledBy">The actor that cancelled it, or null.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
/// <param name="Models">The job's model calls added up, or null when it made none.</param>
/// <param name="WaitingOn">The signal a waiting job is parked on ('model-limit', ...), or null when it isn't waiting.</param>
/// <param name="ModelCalls">The job's last 200 model calls, oldest first (the sheet's list; <see cref="Models"/> counts all of them), or null when it made none.</param>
public sealed record JobRow(JobId Id, JobKind Kind, JobKey Key, ItemId? Item, RepoName? Repo, ServiceName? Service, JobState State,
    string StartedBy, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, IReadOnlyList<JobLine> Progress, int Rounds,
    string? Summary, string? Code, JsonElement? Result, string? CancelledBy, long Sequence, ModelTotals? Models = null, string? WaitingOn = null,
    IReadOnlyList<ContextModelCalled>? ModelCalls = null);
