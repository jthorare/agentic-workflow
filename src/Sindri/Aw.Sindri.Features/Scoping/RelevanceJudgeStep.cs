using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.relevance-judge' on the block runtime (spec 4.2): one judge call per entry of 'fact:lenses', 'parallel' at a time, each request built as its call goes out (the brief is scrubbed and capped once per run, a lens's anchors once per lens), over <see cref="RelevanceJudgeProjection"/> only, so the judge never sees the agent's labels.
/// An escalated answer, a choice that is not one of the three, or one failed call is class 'uncertain' with its reason code; the Step fails only when every call failed, or when the very first call says the judge did not finish
/// (it is not available, so asking the rest would only repeat that). The first call runs alone for that reason. The providers are the runner's resolved list.
/// </summary>
/// <param name="judge">The judge's choose question.</param>
/// <param name="scrubber">Redacts the projection's text before it leaves for the judge.</param>
/// <param name="clock">Times the run and its calls.</param>
/// <param name="logger">Where the progress lines go.</param>
internal sealed partial class RelevanceJudgeStep(IJudgeChoice judge, ITextScrubber scrubber, TimeProvider clock, ILogger<RelevanceJudgeStep> logger) : IBlockStep, IBlockStepId
{
    internal const double DefaultThreshold = 0.6;

    internal const int DefaultParallel = 8;

    internal const int MaxParallel = 16;

    private const string Name = "step.scope.relevance-judge";
    private const string Uncertain = "uncertain";
    private const int ReasonCap = 200;
    private const int ProgressEvery = 25;

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
        var prepared = new Prepared(RelevanceJudgeProjection.Prepare(briefText, scrubber), lenses, new RelevanceJudgeProjection.JudgeSettings(settings.Threshold, settings.Providers), new ConcurrentDictionary<string, string>(StringComparer.Ordinal));
        var started = clock.GetTimestamp();
        LogStart(logger, entries.Count, settings.Parallel);
        var (answers, failure, timings) = await Ask(entries, e => Request(e, prepared), settings.Parallel, started, cancellationToken).ConfigureAwait(false);
        LogEnd(logger, timings.Calls, timings.FirstCallMs, timings.TotalMs, timings.SlowestCallMs);
        if (failure is not null)
        {
            return BlockStepResult.Failed($"{Name}: {failure}");
        }

        return StepFacts.Done("ruled", ("fact:relevanceRulings", Document(entries, answers, settings, timings)));
    }

    private JsonObject Document(IReadOnlyList<RelevanceEntry> entries, ChooseAnswer[] answers, Arguments settings, Timings timings)
    {
        var rulings = new JsonArray();
        for (var i = 0; i < entries.Count; i++)
        {
            ScopeJson.Append(rulings, Ruling(entries[i], answers[i]));
        }

        return new JsonObject
        {
            ["judge"] = new JsonObject { ["providers"] = ScopeJson.ToArray(settings.Providers), ["threshold"] = settings.Threshold },
            ["rulings"] = rulings,
            ["timings"] = new JsonObject
            {
                ["firstCallMs"] = timings.FirstCallMs,
                ["totalMs"] = timings.TotalMs,
                ["calls"] = timings.Calls,
                ["slowestCallMs"] = timings.SlowestCallMs,
            },
        };
    }

    // The brief is prepared once for the run and a lens's anchors once per lens; only the entry's own JSON is scrubbed per request.
    private sealed record Prepared(string Brief, JsonNode? Lenses, RelevanceJudgeProjection.JudgeSettings Settings, ConcurrentDictionary<string, string> AnchorsByLens);

    private ChooseRequest Request(RelevanceEntry entry, Prepared prepared)
    {
        var anchors = prepared.AnchorsByLens.GetOrAdd(
            entry.Lens,
            static (lens, state) => RelevanceJudgeProjection.PrepareAnchors([.. ScopeJson.Strings(ScopeJson.Obj(state.Lenses, lens), "anchors")], state.Scrubber),
            (prepared.Lenses, Scrubber: scrubber));
        return RelevanceJudgeProjection.Request(entry, prepared.Brief, anchors, prepared.Settings, scrubber);
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

    private sealed record Timings(long FirstCallMs, long TotalMs, int Calls, long SlowestCallMs);

    // Each request is built as its call goes out, so the first call does not wait for the rest. The first call runs alone: a judge that did not finish on it is not available, so the rest would only repeat that. A Step whose every call failed fails too.
    private async ValueTask<(ChooseAnswer[] Answers, string? Failure, Timings Timings)> Ask(IReadOnlyList<RelevanceEntry> entries, Func<RelevanceEntry, ChooseRequest> build, int parallel, long started, CancellationToken cancellationToken)
    {
        var answers = new ChooseAnswer[entries.Count];
        var progress = new Progress(entries.Count);
        if (entries.Count == 0)
        {
            return (answers, null, new Timings(0, 0, 0, 0));
        }

        answers[0] = await Timed(entries[0], build, progress, cancellationToken).ConfigureAwait(false);
        var firstCallMs = Elapsed(started);
        if (answers[0].Status == ChooseStatus.Failed && answers[0].Reason.Contains("did not finish", StringComparison.OrdinalIgnoreCase))
        {
            return (answers, answers[0].Reason, progress.Snapshot(firstCallMs, Elapsed(started)));
        }

        await Parallel.ForEachAsync(
            Enumerable.Range(1, entries.Count - 1),
            new ParallelOptions { MaxDegreeOfParallelism = parallel, CancellationToken = cancellationToken },
            async (index, token) => answers[index] = await Timed(entries[index], build, progress, token).ConfigureAwait(false)).ConfigureAwait(false);
        return (answers, answers.All(static a => a.Status == ChooseStatus.Failed) ? $"every judge call failed ({answers[0].Reason})" : null, progress.Snapshot(firstCallMs, Elapsed(started)));
    }

    private long Elapsed(long since) => (long)clock.GetElapsedTime(since).TotalMilliseconds;

    // One call, timed; every 25th ruling and the last one log how far the run is.
    private async ValueTask<ChooseAnswer> Timed(RelevanceEntry entry, Func<RelevanceEntry, ChooseRequest> build, Progress progress, CancellationToken cancellationToken)
    {
        var begun = clock.GetTimestamp();
        var answer = await judge.Choose(build(entry), cancellationToken).ConfigureAwait(false);
        var ruled = progress.Done(Elapsed(begun));
        if (ruled % ProgressEvery == 0 || ruled == progress.Total)
        {
            LogProgress(logger, ruled, progress.Total);
        }

        return answer;
    }

    private sealed class Progress(int total)
    {
        private int _ruled;
        private long _slowest;

        internal int Total { get; } = total;

        internal int Done(long callMs)
        {
            long seen;
            do
            {
                seen = Interlocked.Read(ref _slowest);
            }
            while (callMs > seen && Interlocked.CompareExchange(ref _slowest, callMs, seen) != seen);
            return Interlocked.Increment(ref _ruled);
        }

        internal Timings Snapshot(long firstCallMs, long totalMs) => new(firstCallMs, totalMs, Volatile.Read(ref _ruled), Interlocked.Read(ref _slowest));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "relevance-judge: {Entries} entries, parallel {Parallel}")]
    private static partial void LogStart(ILogger logger, int entries, int parallel);

    [LoggerMessage(Level = LogLevel.Information, Message = "relevance-judge: {Ruled}/{Total} ruled")]
    private static partial void LogProgress(ILogger logger, int ruled, int total);

    [LoggerMessage(Level = LogLevel.Information, Message = "relevance-judge: done, {Calls} calls, firstCallMs={FirstCallMs} totalMs={TotalMs} slowestCallMs={SlowestCallMs}")]
    private static partial void LogEnd(ILogger logger, int calls, long firstCallMs, long totalMs, long slowestCallMs);

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
