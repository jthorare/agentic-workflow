using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>The jobs view in memory: rows a test puts, read through the same port the SQLite view will implement.</summary>
public sealed class InMemoryJobViews : IJobViews
{
    private readonly Dictionary<JobId, JobRow> _rows = [];

    /// <summary>Adds or replaces a row.</summary>
    /// <param name="row">The row.</param>
    public void Put(JobRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        lock (_rows)
        {
            _rows[row.Id] = row;
        }
    }

    public ValueTask<Result<JobRow?>> Get(JobId job, CancellationToken cancellationToken)
    {
        lock (_rows)
        {
            Result<JobRow?> result = _rows.GetValueOrDefault(job);
            return ValueTask.FromResult(result);
        }
    }

    public ValueTask<Result<IReadOnlyList<JobRow>>> Running(CancellationToken cancellationToken)
    {
        lock (_rows)
        {
            return ValueTask.FromResult(Results.Success(JobOrder.OldestFirst(_rows.Values.Where(static r => JobStates.IsActive(r.State)))));
        }
    }

    public ValueTask<Result<IReadOnlyList<JobRow>>> List(JobState? state, JobKind? kind, CancellationToken cancellationToken) =>
        Select(state, kind, oldestFirst: false);

    private ValueTask<Result<IReadOnlyList<JobRow>>> Select(JobState? state, JobKind? kind, bool oldestFirst)
    {
        lock (_rows)
        {
            var rows = _rows.Values.Where(r => (state is null || r.State == state) && (kind is null || r.Kind == kind));
            return ValueTask.FromResult(Results.Success(oldestFirst ? JobOrder.OldestFirst(rows) : JobOrder.NewestFirst(rows)));
        }
    }
}
