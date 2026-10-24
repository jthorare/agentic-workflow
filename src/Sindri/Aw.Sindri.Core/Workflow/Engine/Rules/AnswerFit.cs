using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Which answer fits which suspension (spec §3.8, §5.2, §5.8, §6.4, W7): each row's answer variants, the fix that
/// names them, and the bounds on a grant. The interpreter's Update validator calls it before accepting an answer; an
/// answer that fits runs the suspension's resume action, and '--reject' runs the refusal path instead. A suspension
/// mirroring a child forwards every answer to the child, which checks the fit (§5.8). The recorder reads the same table
/// to name an answer's variant (§9.2), so the two can't drift.
/// </summary>
internal static class AnswerFit
{
    /// <summary>
    /// Whether <paramref name="answer"/> fits the instance's suspension: '--grant' on rounds or no-progress,
    /// '--grant-tokens' on budget, '--text' on needs-answer (from a Step) or no-progress, '--resume [--text]' on a wait's
    /// needs-answer, a timeout, SND-WF-010 or a dispatch refusal, '--accept' or '--reject' on a gate escalation.
    /// '--grant n' and '--grant-tokens n' are any whole number from 1.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="answer">The answer.</param>
    /// <returns>The resume action, or SND-WF-003 whose fix names the answer that fits.</returns>
    public static Result<ResumeAction> Check(WorkflowInstance instance, AnswerGiven answer)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(answer);
        var address = instance.Address.Value;
        if (Unanswerable(instance, address) is { } refused)
        {
            return refused;
        }

        var suspension = instance.Suspension!; // why: Unanswerable refuses an instance with no suspension
        if (suspension.Mirrors is not null)
        {
            return suspension.Resume;
        }

        var row = FitRow.For(suspension, address, instance.Recipe);
        if (!AnswerVariants.Fitting(suspension).Contains(answer.Variant))
        {
            return FitRefusals.Refuse($"{Spelled(answer.Variant)} doesn't fit {address}, {row.Shown}", row.Fix);
        }

        if (OutOfBounds(answer, address) is { } bounded)
        {
            return bounded;
        }

        return answer.Variant == AnswerVariant.Reject ? suspension.Resume with { Kind = ResumeKind.Refuse } : suspension.Resume;
    }

    private static Error? Unanswerable(WorkflowInstance instance, string address)
    {
        if (instance.Ended)
        {
            return FitRefusals.Refuse($"{address} has ended", FitRefusals.Status(address));
        }

        if (instance.State == EngineStates.TakenOver)
        {
            return FitRefusals.Refuse($"{address} is taken over; no answer fits until it is handed back", $"sindri workflow handback {address}");
        }

        return instance.Suspension is null
            ? FitRefusals.Refuse($"{address} is in {instance.State}; nothing there waits for an answer", FitRefusals.Status(address))
            : null;
    }

    // W7 (owner q93): '--grant n' and '--grant-tokens n' are any whole number from 1. Once per park holds because the grant ends the park.
    private static Error? OutOfBounds(AnswerGiven answer, string address)
    {
        if (answer.Variant is not (AnswerVariant.Grant or AnswerVariant.GrantTokens))
        {
            return null;
        }

        var rounds = answer.Variant == AnswerVariant.Grant;
        if (answer.Amount is { } n && n >= 1)
        {
            return null;
        }

        const string limit = "a whole number from 1";   // owner q93, 2026-10-23: no upper bound
        return FitRefusals.Refuse(
            $"{Spelled(answer.Variant)} needs {(rounds ? "rounds" : "tokens")} {limit}",
            FitRefusals.Answer(address, rounds ? "--grant 1" : "--grant-tokens <n>"));
    }

    private static string Spelled(AnswerVariant variant) => variant switch
    {
        AnswerVariant.Text => "--text",
        AnswerVariant.Grant => "--grant",
        AnswerVariant.GrantTokens => "--grant-tokens",
        AnswerVariant.Accept => "--accept",
        AnswerVariant.Reject => "--reject",
        AnswerVariant.Resume or _ => "--resume",
    };
}
