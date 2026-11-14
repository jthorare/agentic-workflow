using System.Linq.Expressions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

// The store and ledger writes of a run (load, begin, node records, commit, finish) are short and idempotent, so a patched run calls them as local activities:
// no task-queue round trip and no extra workflow task per call (#3685). A run that started before the patch replays them as the ordinary activities its history holds.
internal sealed partial class GraphWorkflow
{
    private bool _local;

    private Task Call<TActivities>(Expression<Func<TActivities, Task>> call, bool detached) =>
        _local
            ? Workflow.ExecuteLocalActivityAsync(call, Local(detached))
            : Workflow.ExecuteActivityAsync(call, detached ? _detached : _write);

    private Task<TResult> Call<TActivities, TResult>(Expression<Func<TActivities, Task<TResult>>> call, bool detached) =>
        _local
            ? Workflow.ExecuteLocalActivityAsync(call, Local(detached))
            : Workflow.ExecuteActivityAsync(call, detached ? _detached : _write);

    // The same timeout and retry as GraphActivityOptions.Write and .Detached.
    private static LocalActivityOptions Local(bool detached) => detached
        ? new LocalActivityOptions { StartToCloseTimeout = GraphActivityOptions.Detached.StartToCloseTimeout, RetryPolicy = GraphActivityOptions.Bounded, CancellationToken = CancellationToken.None }
        : new LocalActivityOptions { StartToCloseTimeout = GraphActivityOptions.Write.StartToCloseTimeout, RetryPolicy = GraphActivityOptions.Bounded };
}
