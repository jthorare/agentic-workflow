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

    /// <summary>Store and ledger writes run again until they work, like RecordEvent.</summary>
    public static ActivityOptions Write { get; } = new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), RetryPolicy = Unbounded };

    /// <summary>The same writes after a cancel must not be cancelled with the workflow.</summary>
    public static ActivityOptions Detached { get; } = new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), RetryPolicy = Unbounded, CancellationToken = CancellationToken.None };

    /// <summary>One attempt, because a block may have side effects (notify); a failure is the node's failure.</summary>
    public static ActivityOptions Block { get; } = new() { StartToCloseTimeout = TimeSpan.FromHours(1), RetryPolicy = new RetryPolicy { MaximumAttempts = 1 } };
}
