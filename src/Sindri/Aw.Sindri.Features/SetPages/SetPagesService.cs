using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetPages;

#pragma warning disable MA0182 // until AddSetPages registers it

/// <summary>
/// The work of a SetPages job (pages spec §3.1): reads the live profile again, works the request out, checks it against the map's facts, writes every edit once through <see cref="IProfileIndexWriter"/> with the caller's profile hash,
/// and returns the new profile hash. Every refusal comes before the write. It never approves: the profile waits for the owner's approval as its own step.
/// </summary>
/// <param name="live">The live profile file.</param>
/// <param name="facts">What the current map says about pages.</param>
/// <param name="writer">Writes 'index.pages'.</param>
/// <param name="jobs">The running job: its progress takes the lines.</param>
internal sealed class SetPagesService(IProfileSource live, IMapPageFacts facts, IProfileIndexWriter writer, IJobContextAccessor jobs) : IService<SetPagesRequest, SetPagesResult>
{
    public ValueTask<Result<SetPagesResult>> Handle(SetPagesRequest request, CancellationToken cancellationToken)
    {
        _ = (live, facts, writer, jobs);
        throw new NotSupportedException();   // skeleton: the planner and the write come next
    }
}
#pragma warning restore MA0182
