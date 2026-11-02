using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.PinConcepts;

/// <summary>
/// The checks a PinConcepts start makes inside the gate, before the job is claimed, so their codes are real HTTP errors: the request itself, then the live profile (the ids, the folder rules a removal would drop, a repo's own
/// pin, and the profile's rules over the result: SND-CONTEXT-171 and 173 to 176). The feature keys are checked against the map when the job runs. Nothing is written.
/// </summary>
/// <param name="live">The live profile file.</param>
internal sealed class PinConceptsAdmission(IProfileSource live) : IJobAdmission<PinConceptsRequest>
{
    public async ValueTask<Result<ServiceName?>> Admit(PinConceptsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var plan = PinPlanner.Plan(request, loaded.Value.Profile);
        return plan.IsError ? plan.Error : (ServiceName?)null;
    }
}
