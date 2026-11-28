using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// 'judge trait-check': judges the repo's traits against the edited files' diff (edit), the branch diff (stop) or
/// whole files (scan), in batched Jev requests. A repo without traits, or an edit no trait's paths cover, costs no
/// Jev call, and a verdict cached from an earlier run on the same excerpt is reused (TraitAnswers). A diff reaches Jev
/// redacted (ITraitJudge).
/// </summary>
internal sealed class CheckTraitsService(IProcessRunner runner, ITraitStore traits, ITraitFiles files, ITraitCheckLog log, ITraitCheckSessions sessions, TraitAnswers answers) : IService<CheckTraitsRequest, CheckTraitsResponse>
{
    private readonly TraitWorkspace _workspace = new(new TraitGit(runner), traits, files, log, sessions);
    private readonly TraitVerdicts _verdicts = new(log);

    public async ValueTask<Result<CheckTraitsResponse>> Handle(CheckTraitsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (defaultBudget, minimum, defaultConcurrency) = Defaults(request.Mode);
        if (await _workspace.Plan(request, cancellationToken).ConfigureAwait(false) is not { } planned)
        {
            return new CheckTraitsResponse([]);
        }

        var limits = (request.Budget ?? defaultBudget, request.Concurrency ?? defaultConcurrency);
        var (plan, results) = await answers.Ask(request, planned, limits, cancellationToken).ConfigureAwait(false);
        return _verdicts.Read(request, request.MinProbability ?? minimum, plan, results);
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
}
