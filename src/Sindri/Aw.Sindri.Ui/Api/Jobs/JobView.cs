using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>GET /sindri/jobs/{id} (API P6): one job with its last progress lines and result. The answer's asOf is not read: the status carries the freshness the app shows.</summary>
/// <param name="Id">The job (JOB-…).</param>
/// <param name="Kind">The catalog action that started it, as served.</param>
/// <param name="Key">Its idempotency key within the kind.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Service">The service it runs, or null.</param>
/// <param name="State">running, succeeded, failed or cancelled.</param>
/// <param name="StartedBy">Who started it.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="EndedAt">When it ended, or null while it runs.</param>
/// <param name="Progress">The last progress lines, oldest first.</param>
/// <param name="Rounds">The highest progress round.</param>
/// <param name="Summary">How it ended, in a line, or null while it runs.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
/// <param name="Result">The work's result as served (bounded, scrubbed), or null.</param>
/// <param name="CancelledBy">Who cancelled it, or null.</param>
/// <param name="Actions">The actions allowed now: "CancelJob" while it runs, none after.</param>
/// <param name="Cli">The CLI form of the cancel, or null.</param>
internal sealed record JobView(
    string Id,
    string Kind,
    string Key,
    string? Item,
    string? Repo,
    string? Service,
    string State,
    string StartedBy,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<JobProgressView> Progress,
    int Rounds,
    string? Summary,
    string? Code,
    JsonElement? Result,
    string? CancelledBy,
    IReadOnlyList<string> Actions,
    string? Cli);
