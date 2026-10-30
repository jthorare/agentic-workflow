using System.Globalization;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// Turns a Jev pick call into its 'context.model.called' event (model routing spec §7.1): harness 'jev', the questions in the pack as 'items', how the answers were taken as 'how', the tokens Jev reported (else characters / 4). A pick that was never
/// asked (the profile says off, or there is no key) is a 'not-asked' try with its reason. A call outside a job that answered writes nothing. Never prompt text, answer text, a path or provider error text: names, numbers and codes.
/// </summary>
/// <param name="identity">The Jev model, named in the try and as the answerer.</param>
/// <param name="jobs">The job the call runs under.</param>
/// <param name="log">Where the event goes.</param>
/// <param name="ids">Names each event.</param>
/// <param name="clock">Dates and times the call.</param>
internal sealed class ChoiceCallRecorder(ChoiceModel identity, IJobContextAccessor jobs, IModelCallLog log, IIdGenerator ids, TimeProvider clock)
{
    private const string Harness = "jev";
    private const string Source = "builtin";

    internal ChoiceCall Start(ChoiceKind kind, IReadOnlyList<ChoiceQuestion> questions) => new(kind, questions, clock.GetUtcNow(), clock.GetTimestamp());

    internal async ValueTask Write(ChoiceCall call, Result<IReadOnlyList<ChoiceAnswer>> mapped, ProviderResult? result)
    {
        var job = jobs.Current?.Job;
        if (mapped.IsSuccess && job is null)
        {
            return;
        }

        var ms = (int)Math.Min(clock.GetElapsedTime(call.Stamp).TotalMilliseconds, int.MaxValue);
        await log.Record(Build(call, mapped, (result as BatchDecided)?.Usage, job, ms), CancellationToken.None).ConfigureAwait(false);
    }

    private ContextModelCalled Build(ChoiceCall call, Result<IReadOnlyList<ChoiceAnswer>> mapped, BatchUsage? usage, JobId? job, int ms)
    {
        var (purpose, kind) = call.Kind == ChoiceKind.Labeling ? ("labeling", "labeling") : ("product-surfaces", "productSurfaces");
        var (input, output, basis) = Tokens(call, mapped, usage);
        return new ContextModelCalled(
            ModelCallId.From(ids.NewId().ToString("N", CultureInfo.InvariantCulture)),
            job,
            purpose,
            kind,
            [Attempt(mapped, ms)],
            mapped.IsSuccess ? new ModelCallAnswerer(Harness, identity.Value) : null,
            input,
            output,
            basis,
            call.Questions.Count,
            mapped.IsSuccess ? How(call, mapped.Value) : null,
            call.StartedAt,
            ms);
    }

    private ModelCallTry Attempt(Result<IReadOnlyList<ChoiceAnswer>> mapped, int ms)
    {
        if (mapped.IsSuccess)
        {
            return new ModelCallTry(Harness, identity.Value, ModelCallOutcome.Answered, Reason: null, ms, Source);
        }

        // 'off' and 'no-key' mean the pick was never in force; any other reason is a call that failed.
        var reason = mapped.Error.Message;
        return new ModelCallTry(Harness, identity.Value, Aw.Context.Core.Models.ChoiceReasons.IsQuiet(reason) ? ModelCallOutcome.NotAsked : ModelCallOutcome.Failed, reason, ms, Source);
    }

    // What Jev settled is counted as Jev's; every other question of the pack takes the model's first candidate (a pack's questions are only the clusters that had candidates).
    private static ModelCallHow How(ChoiceCall call, IReadOnlyList<ChoiceAnswer> answers)
    {
        var settled = answers.Count(static a => a.Picked is not null && a.Confidence >= LabelPickLimits.MinConfidence);
        return new ModelCallHow(Jev: settled, FirstCandidate: call.Questions.Count - settled);
    }

    private static (int Input, int Output, string Basis) Tokens(ChoiceCall call, Result<IReadOnlyList<ChoiceAnswer>> mapped, BatchUsage? usage)
    {
        if (usage is not null)
        {
            return ((int)Math.Min(usage.InputTokens, int.MaxValue), (int)Math.Min(usage.OutputTokens, int.MaxValue), ModelTokenBasis.Reported);
        }

        return mapped.IsSuccess
            ? ((call.Questions.Sum(static q => q.Prompt.Length + q.Options.Sum(static o => o.Length)) + 3) / 4, mapped.Value.Count * LabelPickLimits.AnswerTokens, ModelTokenBasis.Estimated)
            : (0, 0, ModelTokenBasis.Estimated);
    }
}
