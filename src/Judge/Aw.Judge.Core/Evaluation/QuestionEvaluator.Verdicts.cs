using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Core.Evaluation;

public sealed partial class QuestionEvaluator
{
    // A provider that did not answer or whose answer is unusable is passed over: true once it is recorded as skipped (and, for a real failure, as a failure row).
    private async ValueTask<bool> Passed<TInput>(
        QuestionDefinition<TInput> question, EvaluationRun run, ProviderAnswer at, StepVerdict verdict, CancellationToken cancellationToken)
        where TInput : class
    {
        var result = at.Result;
        switch (verdict)
        {
            case StepVerdict.Unavailable:
                run.Skip(at.Name, SkipReason.Unavailable, SkipCode(result));
                return true;
            case StepVerdict.NoReasons when JevCannotExplain(question, at.Name):
                // Jev can't give reasons: a problem it spots is a skip for the CLI to explain, not a failure of Jev's.
                run.Skip(at.Name, SkipReason.Unavailable, EvaluationReasons.NoReasons);
                return true;
            case StepVerdict.TimedOut:
                await Fail(question.Name, at.Name, ProviderUnavailable.Timeout, cancellationToken).ConfigureAwait(false);
                run.Skip(at.Name, SkipReason.Timeout);
                return true;
            case StepVerdict.Errored:
            case StepVerdict.OutOfEnum:
            case StepVerdict.NoReasons:
                var limited = result is ProviderErrored { UsageLimit: true };
                await Fail(question.Name, at.Name, FailureCode(verdict, result), cancellationToken).ConfigureAwait(false);
                run.Skip(at.Name, SkipReason.Failed, limited ? EvaluationReasons.UsageLimit : null);
                return true;
            case StepVerdict.Decided:
            case StepVerdict.BelowThreshold:
            default:
                return false;
        }
    }

    // Whether a reasonless answer is Jev's on a question Jev may settle: a skip, not a failure.
    private static bool JevCannotExplain<TInput>(QuestionDefinition<TInput> question, ProviderName provider)
        where TInput : class =>
        question.JevMaySettle && provider == ProviderName.Jev;

    // The finer codes a skip stores: the image ones a provider returns. Other unavailable reasons (no key, not installed) stay as they were.
    private static string? SkipCode(ProviderResult result) => result.ReasonCode switch
    {
        ProviderUnavailable.ImagesOff or ProviderUnavailable.ImageOutOfLimits or ProviderUnavailable.ImageTooLarge => result.ReasonCode,
        _ => null,
    };

    private static StepVerdict Judged<TInput>(QuestionDefinition<TInput> question, EvaluationRun run, ProviderResult result)
        where TInput : class =>
        StepVerdicts.Of(result, question.Outputs, run.Threshold, lacksReasons: result is ProviderDecided decided && question.LacksReasons(decided));

    // The failure row's reason: the provider's own, or the engine's when the provider answered but the answer can't be used.
    private static string FailureCode(StepVerdict verdict, ProviderResult result)
    {
        if (verdict == StepVerdict.NoReasons)
        {
            return EvaluationReasons.NoReasons;
        }

        return result switch
        {
            ProviderDecided => EvaluationReasons.OutOfEnum,
            // The CLI said it hit the usage limit: name that, not its exit code, so a stored failure tells the two apart.
            ProviderErrored { UsageLimit: true } => EvaluationReasons.UsageLimit,
            _ => result.ReasonCode,
        };
    }
}
