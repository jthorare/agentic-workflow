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

    /// <summary>When set, <see cref="RepairRun"/> waits this long before it records the call, as a repair that lands after the run's status does (#2934).</summary>
    public TimeSpan Delay { get; set; }

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

    /// <summary>Waits until at least <paramref name="count"/> calls have landed; fails with the calls seen so far at the deadline.</summary>
    /// <param name="count">How many calls to wait for.</param>
    /// <param name="patience">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>When the calls have landed.</returns>
    public async Task WaitForRuns(int count, TimeSpan patience, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(patience);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        while (Runs.Count < count)
        {
            try
            {
                await Task.Delay(20, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException timedOut) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the repair was asked {Runs.Count} times within {patience.TotalSeconds} s; wanted {count}"), timedOut);
            }
        }
    }

    public ValueTask<Result<int>> RepairAll(CancellationToken cancellationToken) => throw new NotSupportedException();

    public async ValueTask<Result<int>> RepairRun(WorkflowRunId run, CancellationToken cancellationToken)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _runs.Add(run.Value);
        }

        Result<int> answer = Fail is { } failed ? failed : 0;
        return answer;
    }
}
