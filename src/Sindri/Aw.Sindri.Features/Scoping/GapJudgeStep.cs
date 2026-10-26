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
/// 'step.scope.gap-judge' on the block runtime (issue 966, spec 4.5): one judge call per candidate over <see cref="GapJudgeProjection"/>, 'parallel' at a time, each scrubbed and capped as 'step.judge.choose' does.
/// The first call goes alone: a judge that is not there ('did not finish') fails the Step at once. After that a failed or escalated call is an 'uncertain' ruling, and the Step fails only when every call failed.
/// 'fact:gapRulings' is {"judge":{"providers":…,"threshold":…},"rulings":[{"id":…,"ruling":…,"confidence":…,"provider":…,"decisionId":…,"reasonCode":…}]}, in the candidates' order.
/// </summary>
/// <param name="judge">The judge's choose question.</param>
/// <param name="scrubber">Redacts the text before it leaves for the judge.</param>
internal sealed class GapJudgeStep(IJudgeChoice judge, ITextScrubber scrubber) : IBlockStep, IBlockStepId
{
    private const string Name = "step.scope.gap-judge";
    private const string Uncertain = "uncertain";
    private const string Unavailable = "did not finish";
    private const double DefaultThreshold = 0.6;
    private const int DefaultParallel = 4;
    private const int MostParallel = 16;
    private const int TextCap = 200_000;

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = Plan(context);
        if (plan.Failure is not null)
        {
            return BlockStepResult.Failed(plan.Failure);
        }

        var answered = await Answers(plan.Requests, Concurrency(context), cancellationToken).ConfigureAwait(false);
        if (answered.Failure is not null)
        {
            return BlockStepResult.Failed(answered.Failure);
        }

        var rulings = new JsonArray();
        for (var i = 0; i < plan.Candidates.Count; i++)
        {
            ScopeJson.Append(rulings, Ruling(ScopeJson.Str(plan.Candidates[i], "id"), answered.Answers[i]));
        }

        return StepFacts.Done("ruled", ("fact:gapRulings", new JsonObject
        {
            ["judge"] = new JsonObject { ["providers"] = ScopeJson.ToArray(plan.Providers), ["threshold"] = plan.Threshold },
            ["rulings"] = rulings,
        }));
    }

    // The candidates, one request each, and the judge's settings; the reason the Step can't start when a fact is missing or the threshold is out of range.
    private Planned Plan(StepContext context)
    {
        if (StepFacts.Missing(context, "fact:brief", "fact:acceptance", "fact:scopeMap", "fact:lenses", "fact:gapCandidates") is { Count: > 0 } missing)
        {
            return Planned.Cannot($"{Name} needs {string.Join(", ", missing)}, which the instance does not hold");
        }

        var threshold = context.Arguments.TryGetValue("threshold", out var given) && given.ValueKind == JsonValueKind.Number ? given.GetDouble() : DefaultThreshold;
        if (threshold is not (> 0 and <= 1))
        {
            return Planned.Cannot($"{Name}: the threshold {threshold.ToString(CultureInfo.InvariantCulture)} must be above 0 and at most 1");
        }

        var providers = context.Arguments.ContainsKey("providers") ? Strings(context, "providers") : JudgeProviders.Default;
        var candidates = ScopeJson.Items(StepFacts.Read(context, "fact:gapCandidates"), "candidates").ToList();
        return new Planned(candidates, [.. candidates.Select(c => Request(context, c, threshold, providers))], threshold, providers, Failure: null);
    }

    // The first call goes alone, so a judge that isn't there fails the Step at once; the rest run 'parallel' at a time. The Step fails when every call failed.
    private async ValueTask<Answered> Answers(IReadOnlyList<ChooseRequest> requests, int parallel, CancellationToken cancellationToken)
    {
        var answers = new ChooseAnswer[requests.Count];
        if (requests.Count == 0)
        {
            return new Answered(answers, Failure: null);
        }

        answers[0] = await judge.Choose(requests[0], cancellationToken).ConfigureAwait(false);
        if (answers[0].Status == ChooseStatus.Failed && answers[0].Reason.Contains(Unavailable, StringComparison.Ordinal))
        {
            return new Answered(answers, $"{Name}: {answers[0].Reason}");
        }

        var rest = new ParallelOptions { MaxDegreeOfParallelism = parallel, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(Enumerable.Range(1, requests.Count - 1), rest, async (i, token) => answers[i] = await judge.Choose(requests[i], token).ConfigureAwait(false)).ConfigureAwait(false);
        return answers.All(static a => a.Status == ChooseStatus.Failed) ? new Answered(answers, $"{Name}: every judge call failed: {answers[0].Reason}") : new Answered(answers, Failure: null);
    }

    private ChooseRequest Request(StepContext context, JsonNode candidate, double threshold, IReadOnlyList<string> providers)
    {
        var facts = GapJudgeProjection.Facts(StepFacts.Read(context, "fact:brief"), StepFacts.Read(context, "fact:acceptance"), StepFacts.Read(context, "fact:scopeMap"), StepFacts.Read(context, "fact:lenses"), candidate)
            .ToDictionary(static f => f.Key, f => Cap(scrubber.Scrub(f.Value)), StringComparer.Ordinal);
        return new ChooseRequest(GapJudgeProjection.Question, GapJudgeProjection.Choices, facts, threshold, providers);
    }

    // A decided answer in the three rulings is the ruling; anything else (an escalation, a failed call, a word that is not one of the three) is uncertain with the reason the judge gave.
    private static JsonObject Ruling(string? id, ChooseAnswer answer)
    {
        var decided = answer.Status == ChooseStatus.Decided && answer.Choice is { } choice && GapJudgeProjection.Choices.Any(c => string.Equals(c.Name, choice, StringComparison.Ordinal));
        return new JsonObject
        {
            ["id"] = id,
            ["ruling"] = decided ? answer.Choice : Uncertain,
            ["confidence"] = decided ? answer.Confidence : null,
            ["provider"] = decided ? answer.Provider : null,
            ["decisionId"] = answer.DecisionRef,
            ["reasonCode"] = answer.Status switch
            {
                ChooseStatus.Failed => "failed",
                ChooseStatus.Decided => decided ? answer.Reason : "invalid-choice",
                ChooseStatus.Escalated => answer.Reason,
                _ => answer.Reason,
            },
        };
    }

    private static int Concurrency(StepContext context) =>
        context.Arguments.TryGetValue("parallel", out var given) && given.ValueKind == JsonValueKind.Number && given.TryGetInt32(out var count) ? Math.Clamp(count, 1, MostParallel) : DefaultParallel;

    private static string Cap(string text) => text.Length <= TextCap ? text : text[..TextCap];

    private static List<string> Strings(StepContext context, string name) =>
        context.Arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.String).Select(static e => e.GetString() ?? string.Empty)]
            : [];

    private sealed record Planned(IReadOnlyList<JsonNode> Candidates, IReadOnlyList<ChooseRequest> Requests, double Threshold, IReadOnlyList<string> Providers, string? Failure)
    {
        public static Planned Cannot(string failure) => new([], [], 0, [], failure);
    }

    private sealed record Answered(ChooseAnswer[] Answers, string? Failure);
}
