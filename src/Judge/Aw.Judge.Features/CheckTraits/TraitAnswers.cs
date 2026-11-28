using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Traits;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// Asks Jev a plan's questions, reusing cached verdicts (<see cref="ITraitVerdictCache"/>): a trait whose verdict on the
/// same excerpt is cached leaves its batch, the rest of each excerpt's traits are batched again, and only those go to
/// Jev. Each answer Jev gives is cached; a failed or unavailable call caches nothing, and an image excerpt is never
/// cached (its text stands for a picture). The cache is used only while Jev is included, so a check with Jev excluded
/// still judges nothing. One 'verdict-cache' log line per run counts the hits and misses.
/// </summary>
/// <param name="judge">Asks Jev.</param>
/// <param name="cache">The verdicts kept between runs.</param>
/// <param name="log">The trait-check log.</param>
/// <param name="clock">The budget's deadline.</param>
internal sealed class TraitAnswers(ITraitJudge judge, ITraitVerdictCache cache, ITraitCheckLog log, TimeProvider clock)
{
    private static readonly TimeSpan _minimumCallBudget = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _maximumCallBudget = TimeSpan.FromSeconds(60);

    private readonly TraitVerdictKeys _keys = new();

    /// <summary>The plan as asked (its tasks the Jev requests made, its cached verdicts), and Jev's answer to each task, in order.</summary>
    /// <param name="request">The check.</param>
    /// <param name="plan">What to ask.</param>
    /// <param name="limits">The whole check's time budget and how many requests run at once.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The plan and the answers.</returns>
    internal async ValueTask<(TraitPlan Plan, ProviderResult[] Answers)> Ask(CheckTraitsRequest request, TraitPlan plan, (TimeSpan Budget, int Concurrency) limits, CancellationToken cancellationToken)
    {
        var deadline = clock.GetUtcNow() + limits.Budget;
        var hashes = new Dictionary<DiffSource, string>(ReferenceEqualityComparer.Instance);
        List<TraitTask> tasks = [.. plan.Tasks.Select(task => task with { Hash = Hash(hashes, task.Source) })];
        var useCache = await judge.Included(request.Providers, cancellationToken).ConfigureAwait(false);
        var (asked, cached) = useCache ? await Reuse(request.Mode, tasks, cancellationToken).ConfigureAwait(false) : (tasks, []);
        var answers = new ProviderResult[asked.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, answers.Length),
            new ParallelOptions { MaxDegreeOfParallelism = limits.Concurrency, CancellationToken = cancellationToken },
            async (index, token) => answers[index] = await Ask(request, asked[index], deadline, token).ConfigureAwait(false)).ConfigureAwait(false);
        if (useCache)
        {
            await cache.Put(Fresh(asked, answers), cancellationToken).ConfigureAwait(false);
        }

        return (plan with { Tasks = asked, Cached = cached }, answers);
    }

    private static string Hash(Dictionary<DiffSource, string> hashes, DiffSource source)
    {
        if (!hashes.TryGetValue(source, out var hash))
        {
            hash = hashes[source] = TraitVerdictKeys.SourceHash(source);
        }

        return hash;
    }

    private static bool Cacheable(TraitTask task) => task.Images.Count == 0;

    // The cached verdicts out, and each excerpt's remaining traits batched again, in the plan's order.
    private async ValueTask<(List<TraitTask> Asked, List<CachedVerdict> Cached)> Reuse(TraitCheckMode mode, List<TraitTask> tasks, CancellationToken cancellationToken)
    {
        List<string> keys = [.. tasks.Where(Cacheable).SelectMany(task => task.Batch.Select(trait => _keys.Key(trait, task.Hash)))];
        var hits = await cache.Get(keys, cancellationToken).ConfigureAwait(false);
        List<TraitTask> asked = [];
        List<CachedVerdict> cached = [];
        foreach (var excerpt in tasks.GroupBy(task => (task.Source, task.Root, Cacheable(task))))
        {
            var first = excerpt.First();
            if (!Cacheable(first))
            {
                asked.AddRange(excerpt);
                continue;
            }

            List<Trait> misses = [];
            foreach (var trait in excerpt.SelectMany(task => task.Batch))
            {
                if (hits.TryGetValue(_keys.Key(trait, first.Hash), out var answer))
                {
                    cached.Add(new CachedVerdict(first with { Batch = [trait] }, trait, answer));
                }
                else
                {
                    misses.Add(trait);
                }
            }

            asked.AddRange(misses.Count == 0 ? [] : TraitQuestions.Batches(misses).Select(batch => first with { Batch = batch }));
        }

        Counted(mode, cached.Count, keys.Count - cached.Count);
        return (asked, cached);
    }

    private void Counted(TraitCheckMode mode, int hits, int misses)
    {
        if (hits + misses > 0)
        {
            log.Append(new JsonObject { ["event"] = "verdict-cache", ["mode"] = mode.ToString().ToLowerInvariant(), ["hits"] = hits, ["misses"] = misses });
        }
    }

    // The verdicts Jev just gave, by key: only usable choice answers of calls that went through.
    private Dictionary<string, ChoiceBatchAnswer> Fresh(List<TraitTask> asked, ProviderResult[] answers)
    {
        Dictionary<string, ChoiceBatchAnswer> fresh = new(StringComparer.Ordinal);
        for (var i = 0; i < asked.Count; i++)
        {
            if (!Cacheable(asked[i]) || answers[i] is not BatchDecided decided)
            {
                continue;
            }

            foreach (var trait in asked[i].Batch)
            {
                if (decided.Answers.TryGetValue(trait.Name, out var raw) && raw is ChoiceBatchAnswer choice)
                {
                    fresh[_keys.Key(trait, asked[i].Hash)] = choice;
                }
            }
        }

        return fresh;
    }

    private ValueTask<ProviderResult> Ask(CheckTraitsRequest request, TraitTask task, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var remaining = deadline - clock.GetUtcNow();
        var budget = remaining < _minimumCallBudget ? _minimumCallBudget : remaining > _maximumCallBudget ? _maximumCallBudget : remaining;
        List<BatchQuestion> questions = [.. task.Batch.Select(TraitQuestions.For)];
        return judge.Ask(new BatchRequest(State(request, task.Source), questions, budget) { Images = task.Images, ImagesBestEffort = task.Images.Count > 0 }, request.Providers, cancellationToken);
    }

    // What Jev sees: the excerpt. A stop names the ref the branch is compared with.
    private static JsonObject State(CheckTraitsRequest request, DiffSource source)
    {
        var state = new JsonObject { ["mode"] = request.Mode.ToString().ToLowerInvariant() };
        if (request.Mode == TraitCheckMode.Stop)
        {
            state["base"] = TraitWorkspace.BaseRef(request);
        }

        state["source"] = new JsonObject
        {
            ["path"] = source.Path,
            ["startLine"] = source.StartLine,
            ["endLine"] = source.EndLine,
            ["text"] = source.Text,
        };
        return state;
    }
}
