using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetPages;

/// <summary>
/// The checks a SetPages start makes inside the gate, before the job is claimed, so their codes are real HTTP errors: the request itself, then the live profile and the current map's facts through the same planner the job uses
/// (the ids, where each page lives, the repo rule, the concepts, and the write rules over the whole list: SND-CONTEXT-250 to 257). Nothing is written. A dry run is checked exactly like a write.
/// </summary>
/// <param name="live">The live profile file.</param>
/// <param name="facts">What the current map says about pages.</param>
internal sealed class SetPagesAdmission(IProfileSource live, IMapPageFacts facts) : IJobAdmission<SetPagesRequest>
{
    public async ValueTask<Result<ServiceName?>> Admit(SetPagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var map = await facts.Read(cancellationToken).ConfigureAwait(false);
        if (map.IsError)
        {
            return map.Error;
        }

        var plan = PagePlanner.Plan(request, loaded.Value.Profile, map.Value);
        return plan.IsError ? plan.Error : (ServiceName?)null;
    }
}
