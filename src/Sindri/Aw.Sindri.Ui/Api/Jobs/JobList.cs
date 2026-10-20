namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>GET /sindri/jobs: the jobs, newest first, with the served empty sentence. The answer's asOf is not read: the status carries the freshness the app shows.</summary>
/// <param name="Jobs">The rows.</param>
/// <param name="Empty">The sentence for an empty list ("No running jobs."), or null.</param>
internal sealed record JobList(IReadOnlyList<JobRowView> Jobs, string? Empty);
