using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ResumeJob;

namespace Aw.Sindri.Host.Endpoints;

internal static class ResumeJobEndpoint
{
    internal static RouteGroupBuilder MapResumeJob(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/jobs/{jobId}/resume", static async (string jobId, IService<ResumeJobRequest, JobStartedResponse> service, CancellationToken ct) =>
        {
            var job = JobRoute.Job(jobId);
            return job.IsError ? job.ToHttpResult() : (await service.Handle(new ResumeJobRequest(job.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
