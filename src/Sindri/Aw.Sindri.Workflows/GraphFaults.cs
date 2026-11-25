using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Which exceptions in a graph workflow end the run 'failed' (issue 2999), and the text the run gets.</summary>
internal static class GraphFaults
{
    /// <summary>
    /// True for any exception that should end the run: a write out of retries or an exception in workflow code. A cancel, a continue-as-new and the SDK's own
    /// misuse and nondeterminism errors are not faults of the run and keep their meaning.
    /// </summary>
    /// <param name="e">The exception.</param>
    /// <returns>True when the run ends failed.</returns>
    public static bool Ends(Exception e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return !TemporalException.IsCanceledException(e) && e is not (ContinueAsNewException or InvalidWorkflowOperationException or InvalidWorkflowSchedulerException or WorkflowNondeterminismException);
    }

    /// <summary>The text the run's error holds: the activity's own error text, or the exception's type and message for a fault in workflow code.</summary>
    /// <param name="e">The exception.</param>
    /// <returns>The text.</returns>
    public static string Reason(Exception e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return e is ActivityFailureException { InnerException: { } inner } ? inner.Message : e is TemporalException ? e.Message : $"{e.GetType().Name}: {e.Message}";
    }
}
