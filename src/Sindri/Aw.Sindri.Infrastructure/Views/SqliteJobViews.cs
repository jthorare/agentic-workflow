using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>The 'jobs' view read through the views.db reader (API P6 unit B): one job by id, the running ones, and the list by state and kind.</summary>
/// <param name="reader">The views.db read side.</param>
internal sealed class SqliteJobViews(IViewReader reader) : IJobViews
{
    public ValueTask<Result<JobRow?>> Get(JobId job, CancellationToken cancellationToken) =>
        reader.Get<JobRow>(ViewNames.Jobs, job.Value, cancellationToken);

    public async ValueTask<Result<IReadOnlyList<JobRow>>> Running(CancellationToken cancellationToken)
    {
        var running = await reader.Where<JobRow>(ViewNames.Jobs, ViewColumn.State, JobStateNames.Of(JobState.Running), cancellationToken).ConfigureAwait(false);
        var waiting = running.IsError ? running : await reader.Where<JobRow>(ViewNames.Jobs, ViewColumn.State, JobStateNames.Of(JobState.Waiting), cancellationToken).ConfigureAwait(false);
        return running.IsError ? running.Error : waiting.IsError ? waiting.Error : Results.Success(JobOrder.OldestFirst([.. running.Value, .. waiting.Value]));
    }

    public async ValueTask<Result<IReadOnlyList<JobRow>>> List(JobState? state, JobKind? kind, CancellationToken cancellationToken)
    {
        var rows = state is { } wanted
            ? await reader.Where<JobRow>(ViewNames.Jobs, ViewColumn.State, JobStateNames.Of(wanted), cancellationToken).ConfigureAwait(false)
            : kind is { } of
                ? await reader.Where<JobRow>(ViewNames.Jobs, ViewColumn.Kind, of.Value, cancellationToken).ConfigureAwait(false)
                : await reader.List<JobRow>(ViewNames.Jobs, cancellationToken).ConfigureAwait(false);
        return rows.IsError
            ? rows.Error
            : Results.Success(JobOrder.NewestFirst(rows.Value.Where(r => kind is null || r.Kind == kind)));
    }
}
