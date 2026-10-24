using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Traits;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// 'judge trait-check': judges the repo's traits against the edited files' diff (edit), the branch diff (stop) or
/// whole files (scan), in batched Jev requests. A repo without traits, or an edit no trait's paths cover, costs no
/// Jev call. A diff reaches Jev redacted (ITraitJudge).
/// </summary>
internal sealed class CheckTraitsService(IProcessRunner runner, ITraitStore traits, ITraitFiles files, ITraitJudge judge, ITraitCheckLog log, TimeProvider clock) : IService<CheckTraitsRequest, CheckTraitsResponse>
{
    private static readonly TimeSpan _minimumCallBudget = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _maximumCallBudget = TimeSpan.FromSeconds(60);

    private readonly TraitWorkspace _workspace = new(new TraitGit(runner), traits, files, log);
    private readonly TraitVerdicts _verdicts = new(log);

    public async ValueTask<Result<CheckTraitsResponse>> Handle(CheckTraitsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (defaultBudget, minimum, defaultConcurrency) = Defaults(request.Mode);
        var deadline = clock.GetUtcNow() + (request.Budget ?? defaultBudget);
        if (await _workspace.Plan(request, cancellationToken).ConfigureAwait(false) is not { } plan)
        {
            return new CheckTraitsResponse([]);
        }

        var answers = new ProviderResult[plan.Tasks.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, answers.Length),
            new ParallelOptions { MaxDegreeOfParallelism = request.Concurrency ?? defaultConcurrency, CancellationToken = cancellationToken },
            async (index, token) => answers[index] = await Ask(request, plan.Tasks[index], deadline, token).ConfigureAwait(false)).ConfigureAwait(false);
        return _verdicts.Read(request, request.MinProbability ?? minimum, plan, answers);
    }

    // The edit nudge only informs, so it reports earlier; the Stop gate holds the agent back, so it needs a surer call.
    // A scan is run by hand over the whole repo, so it gets an hour and more concurrent Jev calls.
    private static (TimeSpan Budget, double MinProbability, int Concurrency) Defaults(TraitCheckMode mode) => mode switch
    {
        TraitCheckMode.Edit => (TimeSpan.FromSeconds(15), 0.6, 4),
        TraitCheckMode.Stop => (TimeSpan.FromSeconds(40), 0.8, 4),
        TraitCheckMode.Scan => (TimeSpan.FromHours(1), 0.8, 12),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown trait-check mode"),
    };

    private ValueTask<ProviderResult> Ask(CheckTraitsRequest request, TraitTask task, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var remaining = deadline - clock.GetUtcNow();
        var budget = remaining < _minimumCallBudget ? _minimumCallBudget : remaining > _maximumCallBudget ? _maximumCallBudget : remaining;
        List<BatchQuestion> questions = [.. task.Batch.Select(TraitQuestions.For)];
        return judge.Ask(State(request, task.Source), questions, budget, request.Providers, cancellationToken);
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
