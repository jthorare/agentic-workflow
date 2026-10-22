using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetJob;

/// <summary>
/// Reads a job (API P6 unit E): a human or service sees every job, a session only jobs of its own repo or none; any other job is SND-JOB-404, never a
/// hint that it exists. While it runs it carries 'CancelJob' and the CLI line that cancels it.
/// </summary>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller, whose repo scope applies.</param>
internal sealed class GetJobService(IJobViews jobs, IProjectorStatus status, ICallerContextAccessor callers) : IService<GetJobRequest, JobView>
{
    private static readonly ActionId _cancel = ActionId.From("CancelJob");

    public async ValueTask<Result<JobView>> Handle(GetJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("a job needs a caller");
        }

        var asOf = status.Current.AsOf;
        var found = await jobs.Get(request.Job, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } row || !ItemScope.VisibleOrRepoless(caller, row.Repo))
        {
            return SindriErrors.JobNotFound.With(JobRules.NoSuchJob);
        }

        var running = JobStates.IsActive(row.State);   // a waiting job can be cancelled too
        string? cli = null;
        if (running)
        {
            var form = CliForm.Parse("sindri jobs cancel <jobId>").Bind(parsed => parsed.Fill("jobId", row.Id));
            if (form.IsError)
            {
                return form.Error;
            }

            cli = form.Value.Text;
        }

        return new JobView(row.Id, row.Kind, row.Key, row.Item, row.Repo, row.Service, row.State, row.StartedBy, row.StartedAt, row.EndedAt, row.Progress, row.Rounds,
            row.Summary, row.Code, row.Result, row.CancelledBy, running ? [_cancel] : [], cli, asOf);
    }
}
