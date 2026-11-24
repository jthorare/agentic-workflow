using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetSetup;

/// <summary>
/// The setup list without the control plane (first-run spec decision 8): the answer has the shape GET /sindri/setup answers. It reads only the sources the CLI can read in process (the profile and git),
/// through the same <see cref="ISetupFacts"/>, and decides the steps with <see cref="SetupDeriver.WithoutServe"/>. Nothing was projected, so the freshness is all zeros.
/// </summary>
/// <param name="facts">The setup sources; only the repos, the profile and the agent are read.</param>
internal sealed class GetSetupOfflineService(ISetupFacts facts) : IService<GetSetupOfflineRequest, GetSetupResponse>
{
    private static readonly AsOf _nothingProjected = new(Cursor: 0, LedgerHead: 0, ProjectedAt: null, Rebuilding: false, Stalled: false);

    public async ValueTask<Result<GetSetupResponse>> Handle(GetSetupOfflineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var repo = await facts.Repos(cancellationToken).ConfigureAwait(false);
        var profile = await facts.Profile(cancellationToken).ConfigureAwait(false);
        var agent = await facts.Agent(cancellationToken).ConfigureAwait(false);
        return new GetSetupResponse(SetupDeriver.WithoutServe(repo, profile, agent), _nothingProjected);
    }
}
