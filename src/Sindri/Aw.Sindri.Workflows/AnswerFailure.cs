using Aw.Kernel;
using Aw.Sindri.Core;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Turns what Temporal throws for an 'Answer' update into the error the caller sees.</summary>
internal static class AnswerFailure
{
    /// <summary>The error for an exception the update call threw.</summary>
    /// <param name="failure">The exception.</param>
    /// <param name="run">The run id the answer was for.</param>
    /// <returns>The error; never null, so no failure reaches the caller as a 500.</returns>
    public static Error Of(Exception failure, string run)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure switch
        {
            OperationCanceledException or WorkflowUpdateRpcTimeoutOrCanceledException => SindriErrors.EngineUnavailable.With("the run's worker did not answer in time; is 'sindri serve' running? (fix: sindri doctor)"),
            WorkflowUpdateFailedException { InnerException: ApplicationFailureException refused } => string.Equals(refused.ErrorType, AnswerRefusal.NotWaiting, StringComparison.Ordinal)
                ? SindriErrors.GraphNotWaiting.With(refused.Message)
                : SindriErrors.InvalidInput.With(refused.Message),
            WorkflowUpdateFailedException => SindriErrors.GraphRunEnded.With($"the run '{run}' ended or was cancelled before it took the answer"),
            RpcException { Code: RpcException.StatusCode.NotFound } => SindriErrors.GraphRunEnded.With($"the run '{run}' already ended; there is nothing to answer"),
            _ => SindriErrors.EngineUnavailable.With(failure.Message),
        };
    }
}
