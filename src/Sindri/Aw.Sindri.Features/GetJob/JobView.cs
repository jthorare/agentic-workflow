using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetJob;

/// <summary>A job as 'GET /sindri/jobs/{jobId}' serves it (API spec §3.1, API P6).</summary>
/// <param name="Id">The job.</param>
/// <param name="Kind">The catalog action that started it.</param>
/// <param name="Key">Its idempotency key within the kind.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Service">The service it runs, or null.</param>
/// <param name="State">Where it is.</param>
/// <param name="StartedBy">Who started it.</param>
/// <param name="StartedAt">When.</param>
/// <param name="EndedAt">When it ended, or null while running.</param>
/// <param name="Progress">The last 200 progress lines.</param>
/// <param name="Rounds">The highest progress round.</param>
/// <param name="Summary">How it ended in a line, or null while running.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
/// <param name="Result">The work's result (bounded, scrubbed), or null.</param>
/// <param name="CancelledBy">Who cancelled it, or null.</param>
/// <param name="Actions">'CancelJob' while running, none otherwise.</param>
/// <param name="Cli">'sindri jobs cancel JOB-…' while running (through CliForm.Fill), else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
/// <param name="Models">The job's model calls added up, or null when it made none.</param>
/// <param name="WaitingOn">The signal a waiting job is parked on ('model-limit', ...), or null.</param>
/// <param name="ModelCalls">The job's last 200 model calls, oldest first (empty when it made none).</param>
/// <param name="ModelCallCount">How many model calls the job made in all, so a client can say 'showing the last 200 of N'.</param>
public sealed record JobView(JobId Id, JobKind Kind, JobKey Key, ItemId? Item, RepoName? Repo, ServiceName? Service, JobState State, string StartedBy,
    DateTimeOffset StartedAt, DateTimeOffset? EndedAt, IReadOnlyList<JobLine> Progress, int Rounds, string? Summary, string? Code, JsonElement? Result,
    string? CancelledBy, IReadOnlyList<ActionId> Actions, string? Cli, AsOf AsOf,
    ModelTotals? Models = null, string? WaitingOn = null, IReadOnlyList<JobModelCall>? ModelCalls = null, int ModelCallCount = 0);
