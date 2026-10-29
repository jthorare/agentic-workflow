namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>What POST /sindri/jobs/{id}/resume answers: the command accepted. The job's links are not read.</summary>
/// <param name="Accepted">The command's id and ledger sequence.</param>
internal sealed record JobResumedView(JobResumeAccepted Accepted);
