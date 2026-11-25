using Temporalio.Common;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How the graph workflows call their activities; <see cref="GraphWorkflow"/> and <see cref="GraphIterationWorkflow"/> share them.</summary>
internal static class GraphActivityOptions
{
    /// <summary>Retried without end, backing off to 30 seconds.</summary>
    public static RetryPolicy Unbounded { get; } = new()
    {
        InitialInterval = TimeSpan.FromMilliseconds(100),
        BackoffCoefficient = 2,
        MaximumInterval = TimeSpan.FromSeconds(30),
        MaximumAttempts = 0,
    };

    /// <summary>
    /// Ten attempts with the same backoff (about a minute in all). why: a store that stays broken (disk full, a locked database) must not keep a run 'running' for ever (issue 2999);
    /// after the last attempt the workflow ends the run 'failed' with the error text and the next run of the workflow gets its turn.
    /// </summary>
    public static RetryPolicy Bounded { get; } = new()
    {
        InitialInterval = TimeSpan.FromMilliseconds(100),
        BackoffCoefficient = 2,
        MaximumInterval = TimeSpan.FromSeconds(30),
        MaximumAttempts = 10,
    };

    /// <summary>Store and ledger writes run again a few times, like RecordEvent, then fail the run.</summary>
    public static ActivityOptions Write { get; } = new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), RetryPolicy = Bounded };

    /// <summary>The same writes after a cancel must not be cancelled with the workflow.</summary>
    public static ActivityOptions Detached { get; } = new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), RetryPolicy = Bounded, CancellationToken = CancellationToken.None };

    /// <summary>One attempt, because a block may have side effects (notify); a failure is the node's failure.</summary>
    public static ActivityOptions Block { get; } = new() { StartToCloseTimeout = TimeSpan.FromHours(1), RetryPolicy = new RetryPolicy { MaximumAttempts = 1 } };
}
