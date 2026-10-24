using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListJobs;

/// <summary>Lists jobs (API P6 unit E; provisional owner default): by state and kind, newest first, a session only those of its repo or none. Stale views replace the empty sentence and keep the rows.</summary>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
/// <param name="kinds">The kinds of job this build registers; a kind filter must name one.</param>
internal sealed class ListJobsService(IJobViews jobs, IProjectorStatus status, ICallerContextAccessor callers, IJobKinds kinds) : IService<ListJobsRequest, JobList>
{
    public async ValueTask<Result<JobList>> Handle(ListJobsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the job list needs a caller");
        }

        if (request.Kind is { } kind)
        {
            if (!kinds.Known.Contains(kind))
            {
                return SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(kind.Value), 80)}' isn't a job kind (fix: {string.Join(", ", kinds.Known.Select(static k => k.Value))})");
            }
        }

        var current = status.Current;
        var rows = await jobs.List(request.State, request.Kind, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        IReadOnlyList<JobSummary> shown = [.. rows.Value.Select(static r => new JobSummary(r.Id, r.Kind, r.Key, r.Item, r.Repo, r.Service, r.State, r.StartedBy, r.StartedAt, r.EndedAt, r.Rounds, r.Summary, r.Code))];
        return new JobList(shown, EmptyText.For(current, shown.Count == 0, () => EmptyText.Jobs(request.State, request.Kind)), current.AsOf);
    }
}
