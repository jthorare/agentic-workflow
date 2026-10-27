using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>
/// 'judge eval': scores one provider, called directly with no chain and no decisions rows, against a question's labelled
/// items. The answers can be recorded to a file and a later run replayed from it with no provider call. Every file this
/// writes (the record, the .json and .md reports) is redacted, values and keys, before it is written.
/// </summary>
internal sealed class EvaluateQuestionService(
    IEvalStore store, IProviderCatalog catalog, IEvalFiles files, IInputRedactor redactor, TimeProvider clock, JsonSerializerOptions json)
    : IService<EvaluateQuestionRequest, EvaluateQuestionResponse>
{
    public async ValueTask<Result<EvaluateQuestionResponse>> Handle(EvaluateQuestionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = Plan(request);
        if (plan.IsError)
        {
            return Results.Failure<EvaluateQuestionResponse>(plan.Error);
        }

        var (question, variant) = plan.Value;
        var labeled = await Items(request, cancellationToken).ConfigureAwait(false);
        if (labeled.IsError)
        {
            return Results.Failure<EvaluateQuestionResponse>(labeled.Error);
        }

        var results = request.ReplayFile is { } replay
            ? await Replayed(replay, labeled.Value, cancellationToken).ConfigureAwait(false)
            : await Asked(request, question, variant, labeled.Value, cancellationToken).ConfigureAwait(false);
        if (results.IsError)
        {
            return Results.Failure<EvaluateQuestionResponse>(results.Error);
        }

        return await Report(request, question, results.Value, cancellationToken).ConfigureAwait(false);
    }

    // What the request names, checked before any store or provider is touched.
    private static Result<(ScoringQuestion Question, Func<JsonNode?, JsonNode?> Variant)> Plan(EvaluateQuestionRequest request)
    {
        if (request.Providers is { } allowed && !allowed.Contains(request.Provider))
        {
            return JudgeErrors.ProviderExcluded.With($"{ProviderNames.Name(request.Provider)} is excluded by the providers allowlist");
        }

        if (QuestionCatalog.Scoring(request.Question) is not { } question)
        {
            return JudgeErrors.Invalid.With($"unknown question: {request.Question}");
        }

        if (EvalVariants.Find(request.Question, request.Variant) is not { } variant)
        {
            return JudgeErrors.Invalid.With($"unknown variant for {request.Question}: {request.Variant}");
        }

        return request.Collapse is { } collapse && !question.Outputs.Contains(collapse, StringComparer.Ordinal)
            ? JudgeErrors.Invalid.With($"--collapse must be one of: {string.Join(", ", question.Outputs)} (got {collapse})")
            : Results.Success((question, variant));
    }

    private async ValueTask<Result<IReadOnlyList<LabeledItem>>> Items(EvaluateQuestionRequest request, CancellationToken cancellationToken)
    {
        var labeled = await store.Labeled(request.Question, request.Labels, cancellationToken).ConfigureAwait(false);
        if (labeled.IsError)
        {
            return labeled;
        }

        var items = EvalVariants.NeedsStoredDiff(request.Question, request.Variant)
            ? WithStoredDiff(labeled.Value)
            : labeled.Value;
        return items.Count == 0
            ? JudgeErrors.Invalid.With($"no labeled items for {request.Question} (run: judge label import, judge label outcomes, judge adjudicate {request.Question})")
            : Results.Success(items);
    }

    private static IReadOnlyList<LabeledItem> WithStoredDiff(IReadOnlyList<LabeledItem> items) =>
        [.. items.Where(l => EvalVariants.HasStoredDiff(StoredInput(l.Item)))];

    private static JsonNode? StoredInput(EvalItem item) => InputJson.TryParse(item.InputJson, out var node) ? node : null;

    // The last row per item wins; the labels always come from the store, for the source asked.
    private async ValueTask<Result<IReadOnlyList<EvalResult>>> Replayed(string file, IReadOnlyList<LabeledItem> items, CancellationToken cancellationToken)
    {
        var lines = await files.ReadLines(file, cancellationToken).ConfigureAwait(false);
        if (lines.IsError)
        {
            return JudgeErrors.Invalid.With($"cannot read replay file: {lines.Error.Message}");
        }

        var latest = new Dictionary<string, EvalResult>(StringComparer.Ordinal);
        foreach (var (line, number) in lines.Value.Select((line, index) => (line, index + 1)))
        {
            if (EvalRecordLine.TryRead(line) is not { } row)
            {
                return JudgeErrors.Invalid.With(string.Create(CultureInfo.InvariantCulture, $"cannot read replay file: line {number} is not an eval record"));
            }

            latest[row.ItemId] = row;
        }

        return Results.Success<IReadOnlyList<EvalResult>>([.. items.Where(i => latest.ContainsKey(i.Item.Id.Value)).Select(i => latest[i.Item.Id.Value] with { Label = i.Label })]);
    }

    private async ValueTask<Result<IReadOnlyList<EvalResult>>> Asked(
        EvaluateQuestionRequest request, ScoringQuestion question, Func<JsonNode?, JsonNode?> variant, IReadOnlyList<LabeledItem> items, CancellationToken cancellationToken)
    {
        var found = catalog.Provider(request.Provider, request.Providers);
        if (found.IsError)
        {
            return Results.Failure<IReadOnlyList<EvalResult>>(found.Error);
        }

        var provider = found.Value;
        List<EvalResult> results = [];
        foreach (var labeled in items)
        {
            var input = InputJson.TryParse(labeled.Item.InputJson, out var node) ? variant(node) : null;
            var asked = question.Ask(input, json);
            var start = clock.GetTimestamp();
            var answer = asked is null
                ? new ProviderErrored("invalid-input")
                : await provider.Decide(asked, input, question.TimeBudget, cancellationToken).ConfigureAwait(false);
            var row = new EvalResult(labeled.Item.Id.Value, labeled.Label, answer, (long)clock.GetElapsedTime(start).TotalMilliseconds);
            results.Add(row);
            if (request.RecordFile is { } recordFile)
            {
                var written = await files.AppendLine(recordFile, Redacted(EvalRecordLine.ToNode(row)), cancellationToken).ConfigureAwait(false);
                if (written.IsError)
                {
                    return Results.Failure<IReadOnlyList<EvalResult>>(written.Error);
                }
            }
        }

        return Results.Success<IReadOnlyList<EvalResult>>(results);
    }

    private async ValueTask<Result<EvaluateQuestionResponse>> Report(
        EvaluateQuestionRequest request, ScoringQuestion question, IReadOnlyList<EvalResult> asked, CancellationToken cancellationToken)
    {
        var results = EvalScoring.Collapse(asked, request.Collapse);
        var setup = new EvalSetup(request.Question, ProviderNames.Name(request.Provider), request.Variant, question.Threshold, request.Collapse);
        var report = EvalScoring.Score(setup, results, request.Labels);
        var outcome = await store.Labeled(request.Question, LabelSource.Outcome, cancellationToken).ConfigureAwait(false);
        var adjudicator = await store.Labeled(request.Question, LabelSource.Adjudicator, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError || adjudicator.IsError)
        {
            return Results.Failure<EvaluateQuestionResponse>(outcome.IsError ? outcome.Error : adjudicator.Error);
        }

        report = EvalScoring.WithSources(report, setup, results, outcome.Value, adjudicator.Value);
        var stem = EvalReportText.FileStem(report, clock.GetUtcNow().ToUnixTimeMilliseconds());
        var data = await files.WriteReport($"{stem}.json", Redacted(EvalReportText.ToNode(report)), cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return Results.Failure<EvaluateQuestionResponse>(data.Error);
        }

        var text = await files.WriteReport($"{stem}.md", RedactedText(EvalReportText.Render(report)), cancellationToken).ConfigureAwait(false);
        return text.IsError ? Results.Failure<EvaluateQuestionResponse>(text.Error) : new EvaluateQuestionResponse(report, text.Value);
    }

    // Values and keys both go through the redactor, before anything is written.
    private string Redacted(JsonNode node) => InputJson.Write(redactor.Redact(node), json);

    private string RedactedText(string text) => redactor.Redact(JsonValue.Create(text)) is JsonValue value && value.TryGetValue<string>(out var redacted) ? redacted : "";
}
