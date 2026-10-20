namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>One row of GET /sindri/jobs (API P6). State is read as a string, so a state this build doesn't know renders as itself instead of failing the read.</summary>
/// <param name="Id">The job (JOB-…).</param>
/// <param name="Kind">The catalog action that started it, as served ("RunService"); an unknown kind renders by this label.</param>
/// <param name="Key">Its idempotency key within the kind.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Service">The service it runs, or null.</param>
/// <param name="State">running, succeeded, failed or cancelled.</param>
/// <param name="StartedBy">Who started it ("service:host", "human").</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="EndedAt">When it ended, or null while it runs.</param>
/// <param name="Rounds">The highest progress round.</param>
/// <param name="Summary">How it ended, in a line, or null while it runs.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
internal sealed record JobRowView(
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
    int Rounds,
    string? Summary,
    string? Code);
