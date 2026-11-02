using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ResumeJob;

/// <summary>Wake a job parked on 'model-limit' (POST /sindri/jobs/{jobId}/resume, 'sindri jobs resume'; model routing spec §5.3; desk): a human's Resume once a model harness is back. Answers the job, as a start does.</summary>
/// <param name="Job">The job that waits for a harness.</param>
public sealed record ResumeJobRequest(JobId Job) : ICommand<JobStartedResponse>;
