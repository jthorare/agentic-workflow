using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Exceptions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>What the answer update throws becomes a clean error, never a 500 (issue 2258, PR 9).</summary>
public sealed class AnswerFailureTests
{
    private static ApplicationFailureException Refusal(string type) => new("no", errorType: type, nonRetryable: true);

    [Fact, Trait("Lane", "unit")]
    public void A_timeout_or_cancel_is_the_engine_being_unavailable()
    {
        Assert.Equal(SindriErrors.EngineUnavailable.Code, AnswerFailure.Of(new OperationCanceledException(), "wf@1:1").Code, StringComparer.Ordinal);
        Assert.Equal(SindriErrors.EngineUnavailable.Code, AnswerFailure.Of(new WorkflowUpdateRpcTimeoutOrCanceledException(new OperationCanceledException()), "wf@1:1").Code, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_validator_refusal_keeps_its_kind()
    {
        Assert.Equal(SindriErrors.GraphNotWaiting.Code, AnswerFailure.Of(new WorkflowUpdateFailedException(Refusal(AnswerRefusal.NotWaiting)), "wf@1:1").Code, StringComparer.Ordinal);
        Assert.Equal(SindriErrors.InvalidInput.Code, AnswerFailure.Of(new WorkflowUpdateFailedException(Refusal(AnswerRefusal.BadAnswer)), "wf@1:1").Code, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_update_that_failed_without_an_application_failure_means_the_run_ended()
    {
        var error = AnswerFailure.Of(new WorkflowUpdateFailedException(new InvalidOperationException("closed")), "wf@1:1");

        Assert.Equal(SindriErrors.GraphRunEnded.Code, error.Code, StringComparer.Ordinal);
        Assert.Contains("wf@1:1", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_workflow_means_the_run_ended() =>
        Assert.Equal(SindriErrors.GraphRunEnded.Code, AnswerFailure.Of(new RpcException(RpcException.StatusCode.NotFound, "gone", []), "wf@1:1").Code, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Any_other_temporal_exception_is_the_engine_being_unavailable()
    {
        Assert.Equal(SindriErrors.EngineUnavailable.Code, AnswerFailure.Of(new ApplicationFailureException("odd"), "wf@1:1").Code, StringComparer.Ordinal);
        Assert.Equal(SindriErrors.EngineUnavailable.Code, AnswerFailure.Of(new RpcException(RpcException.StatusCode.Unavailable, "down", []), "wf@1:1").Code, StringComparer.Ordinal);
    }
}
