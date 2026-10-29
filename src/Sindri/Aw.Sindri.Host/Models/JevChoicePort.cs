#pragma warning disable CS9113 // the event is written by the next commit
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The context engine's choice port over Judge's batch classifier (model routing spec §6.7): Jev picks one option for each question of a pack. It sends only the strings Context already scrubbed (the judge's own redactor and cap run on top), reads the
/// pick setting from the APPROVED profile at every call (never the live file), and writes one 'context.model.called' event per call.
/// </summary>
/// <param name="classifier">Judge's batch classifier (built on first use).</param>
/// <param name="providers">The judge's 'providers' config (its 'inputCapChars'), or null for the defaults.</param>
/// <param name="models">'context.models' of the approved profile.</param>
/// <param name="jobs">The job the call runs under.</param>
/// <param name="log">Where the call's event is written.</param>
/// <param name="ids">Names each call's event.</param>
/// <param name="clock">Dates and times the call.</param>
internal sealed class JevChoicePort(
    Func<IBatchClassifier> classifier,
    Func<CancellationToken, ValueTask<ProvidersConfig?>> providers,
    IApprovedModels models,
    IJobContextAccessor jobs,
    IModelCallLog log,
    IIdGenerator ids,
    TimeProvider clock) : IChoicePort
{
    // The input the judge refused as such: the labeler may split the pack. Everything else that is an error or an unavailable provider would fail the same way for the rest of the pass.
    private static readonly string[] _refused = ["http-400", "http-413", "http-422", "unparseable-answer"];

    public ChoiceModel Identity { get; } = new(JevModel.Id);

    public async ValueTask<int> InputCapChars(CancellationToken cancellationToken) =>
        (await providers(cancellationToken).ConfigureAwait(false))?.InputCapChars ?? ProvidersConfig.DefaultInputCapChars;

    public async ValueTask<Result<IReadOnlyList<ChoiceAnswer>>> Pick(ChoiceKind kind, IReadOnlyList<ChoiceQuestion> questions, int capChars, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(questions);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await IsOn(kind, cancellationToken).ConfigureAwait(false))
        {
            return ContextErrors.ChoiceUnavailable.With(ChoiceReasons.Off);
        }

        if (questions.Sum(LabelChars) > LabelPickLimits.CallChars(capChars))
        {
            return ContextErrors.ChoiceRejected.With(ChoiceReasons.OverCap);
        }

        var batch = new BatchRequest(State: null, [.. questions.Select(Wire)], LabelPickLimits.Budget);
        var result = await classifier().Classify(batch, await providers(cancellationToken).ConfigureAwait(false), allowed: null, cancellationToken).ConfigureAwait(false);
        return Map(result, questions);
    }

    // The approved profile, read at every call: an edited live file that says 'jev' makes no Jev call until it is approved. The product-surface pick has no setting until R-8 adds it.
    private async ValueTask<bool> IsOn(ChoiceKind kind, CancellationToken cancellationToken) =>
        kind == ChoiceKind.Labeling && (await models.Current(cancellationToken).ConfigureAwait(false)).LabelPick == LabelPick.Jev;

    private static int LabelChars(ChoiceQuestion question) => question.Prompt.Length + question.Options.Sum(static o => o.Length);

    // The option text is the criterion's key; its value is a short fixed word, so the options are sent once and the pack stays inside the share of the cap the labeler sized it for.
    private static ChoiceBatchQuestion Wire(ChoiceQuestion question) => new(
        question.Key,
        question.Prompt,
        question.Options.Distinct(StringComparer.Ordinal).Select(static (option, i) => (Option: option, Index: i)).ToDictionary(static o => o.Option, static o => "option " + (o.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal));

    private static Result<IReadOnlyList<ChoiceAnswer>> Map(ProviderResult result, IReadOnlyList<ChoiceQuestion> questions) => result switch
    {
        BatchDecided decided => Aw.Kernel.Results.Success<IReadOnlyList<ChoiceAnswer>>([.. questions.Select(q => Answer(q, decided)).OfType<ChoiceAnswer>()]),
        ProviderUnavailable { ReasonCode: "no-api-key" } => ContextErrors.ChoiceUnavailable.With(ChoiceReasons.NoKey),
        ProviderUnavailable { ReasonCode: ProviderUnavailable.Timeout } => ContextErrors.ChoiceRejected.With(ProviderUnavailable.Timeout),
        ProviderErrored errored when _refused.Contains(errored.ReasonCode, StringComparer.Ordinal) => ContextErrors.ChoiceRejected.With(errored.ReasonCode),
        _ => ContextErrors.ChoiceUnavailable.With(result.ReasonCode),
    };

    // A question the answer leaves out (or answers in a shape the judge couldn't use) has no answer; a choice that is none of the options is an answer with nothing picked.
    private static ChoiceAnswer? Answer(ChoiceQuestion question, BatchDecided decided) =>
        decided.Answers.GetValueOrDefault(question.Key) is ChoiceBatchAnswer choice
            ? new ChoiceAnswer(question.Key, question.Options.Contains(choice.Choice, StringComparer.Ordinal) ? choice.Choice : null, choice.Confidence)
            : null;
}
