using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>An <see cref="IRunItemRepair"/> that records the runs it was asked to repair and can be told to fail.</summary>
internal sealed class SpyRunItemRepair : IRunItemRepair
{
    private readonly List<string> _runs = [];
    private readonly Lock _gate = new();

    /// <summary>When set, <see cref="RepairRun"/> answers this error.</summary>
    public Error? Fail { get; set; }

    /// <summary>The runs <see cref="RepairRun"/> got, in order.</summary>
    public IReadOnlyList<string> Runs
    {
        get
        {
            lock (_gate)
            {
                return [.. _runs];
            }
        }
    }

    public ValueTask<Result<int>> RepairAll(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<int>> RepairRun(WorkflowRunId run, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _runs.Add(run.Value);
        }

        Result<int> answer = Fail is { } failed ? failed : 0;
        return ValueTask.FromResult(answer);
    }
}
