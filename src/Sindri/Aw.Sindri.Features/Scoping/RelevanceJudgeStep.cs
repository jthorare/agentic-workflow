using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.relevance-judge' on the block runtime (spec 4.2): one judge call per entry of 'fact:lenses', 'parallel' at a time, over <see cref="RelevanceJudgeProjection"/> only, so the judge never sees the agent's labels.
/// An escalated answer, a choice that is not one of the three, or one failed call is class 'uncertain' with its reason code; the Step fails only when every call failed, or when the very first call says the judge did not finish
/// (it is not available, so asking the rest would only repeat that). The first call runs alone for that reason. The providers are the runner's resolved list.
/// </summary>
/// <param name="judge">The judge's choose question.</param>
/// <param name="scrubber">Redacts the projection's text before it leaves for the judge.</param>
internal sealed class RelevanceJudgeStep(IJudgeChoice judge, ITextScrubber scrubber) : IBlockStep, IBlockStepId
{
    internal const double DefaultThreshold = 0.6;

    internal const int DefaultParallel = 4;

    internal const int MaxParallel = 16;

    private const string Name = "step.scope.relevance-judge";
    private const string Uncertain = "uncertain";
    private const int ReasonCap = 200;

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Missing(context, "fact:brief", "fact:lenses") is { Count: > 0 } missing)
        {
            return BlockStepResult.Failed($"{Name} needs {string.Join(", ", missing)}, which the instance does not hold");
        }

        var settings = Settings(context);
        if (settings.Threshold is <= 0 or > 1)
        {
            return BlockStepResult.Failed($"{Name}: the threshold {settings.Threshold.ToString(CultureInfo.InvariantCulture)} must be above 0 and at most 1");
        }

        var lenses = StepFacts.Read(context, "fact:lenses");
        var briefText = ScopeJson.Str(StepFacts.Read(context, "fact:brief"), "text") ?? string.Empty;
        var entries = RelevanceEntries.Of(lenses);
        var requests = entries.Select(e => RelevanceJudgeProjection.Request(e, briefText, [.. ScopeJson.Strings(ScopeJson.Obj(lenses, e.Lens), "anchors")], new RelevanceJudgeProjection.JudgeSettings(settings.Threshold, settings.Providers), scrubber)).ToList();
        var (answers, failure) = await Ask(requests, settings.Parallel, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return BlockStepResult.Failed($"{Name}: {failure}");
        }

        var rulings = new JsonArray();
        for (var i = 0; i < entries.Count; i++)
        {
            ScopeJson.Append(rulings, Ruling(entries[i], answers[i]));
        }

        return StepFacts.Done("ruled", ("fact:relevanceRulings", new JsonObject
        {
            ["judge"] = new JsonObject { ["providers"] = ScopeJson.ToArray(settings.Providers), ["threshold"] = settings.Threshold },
            ["rulings"] = rulings,
        }));
    }

    private sealed record Arguments(double Threshold, IReadOnlyList<string> Providers, int Parallel);

    private static Arguments Settings(StepContext context)
    {
        var threshold = context.Arguments.TryGetValue("threshold", out var given) && given.ValueKind == JsonValueKind.Number ? given.GetDouble() : DefaultThreshold;
        var providers = Providers(context);
        var parallel = context.Arguments.TryGetValue("parallel", out var width) && width.ValueKind == JsonValueKind.Number && width.TryGetInt32(out var asked) ? Math.Clamp(asked, 1, MaxParallel) : DefaultParallel;
        return new Arguments(threshold, providers, parallel);
    }

    private static IReadOnlyList<string> Providers(StepContext context)
    {
        if (context.Arguments.TryGetValue("providers", out var listed) && listed.ValueKind == JsonValueKind.Array)
        {
            return [.. listed.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.String).Select(static e => e.GetString() ?? string.Empty)];
        }

        return JudgeProviders.Default;
    }

    // The first call runs alone: a judge that did not finish on it is not available, so the rest would only repeat that. A Step whose every call failed fails too.
    private async ValueTask<(ChooseAnswer[] Answers, string? Failure)> Ask(List<ChooseRequest> requests, int parallel, CancellationToken cancellationToken)
    {
        var answers = new ChooseAnswer[requests.Count];
        if (requests.Count == 0)
        {
            return (answers, null);
        }

        answers[0] = await judge.Choose(requests[0], cancellationToken).ConfigureAwait(false);
        if (answers[0].Status == ChooseStatus.Failed && answers[0].Reason.Contains("did not finish", StringComparison.OrdinalIgnoreCase))
        {
            return (answers, answers[0].Reason);
        }

        await Parallel.ForEachAsync(
            Enumerable.Range(1, requests.Count - 1),
            new ParallelOptions { MaxDegreeOfParallelism = parallel, CancellationToken = cancellationToken },
            async (index, token) => answers[index] = await judge.Choose(requests[index], token).ConfigureAwait(false)).ConfigureAwait(false);
        return (answers, answers.All(static a => a.Status == ChooseStatus.Failed) ? $"every judge call failed ({answers[0].Reason})" : null);
    }

    private JsonObject Ruling(RelevanceEntry entry, ChooseAnswer answer)
    {
        var decided = answer.Status == ChooseStatus.Decided;
        if (decided && answer.Choice is { } choice && RelevanceJudgeProjection.Choices.Any(c => string.Equals(c.Name, choice, StringComparison.Ordinal)))
        {
            return Row(entry, new Verdict(choice, answer.Confidence, answer.Provider), answer.DecisionRef, Reason(answer.Reason));
        }

        var code = decided ? "invalid-choice" : Reason(answer.Reason);
        return Row(entry, new Verdict(Uncertain, Confidence: null, Provider: null), answer.DecisionRef, code);
    }

    private string Reason(string reason)
    {
        var clean = scrubber.Scrub(reason).Trim();
        return clean.Length <= ReasonCap ? clean : clean[..ReasonCap];
    }

    private sealed record Verdict(string Class, double? Confidence, string? Provider);

    private static JsonObject Row(RelevanceEntry entry, Verdict verdict, string? decisionId, string reasonCode) => new()
    {
        ["id"] = entry.Id,
        ["class"] = verdict.Class,
        ["confidence"] = verdict.Confidence,
        ["provider"] = verdict.Provider,
        ["decisionId"] = decisionId,
        ["reasonCode"] = reasonCode,
    };
}
