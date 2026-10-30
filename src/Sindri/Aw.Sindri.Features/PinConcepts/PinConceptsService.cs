using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.PinConcepts;

/// <summary>
/// The work of a PinConcepts job (concept pins spec §3.6): checks the pins with the profile's rules, writes them through <see cref="Core.Setup.IProfileConceptWriter"/>, records 'context.pin.written' with the pins and the
/// actor, and returns the new profile hash. It never approves.
/// </summary>
internal sealed class PinConceptsService : IService<PinConceptsRequest, PinConceptsResult>
{
    public ValueTask<Result<PinConceptsResult>> Handle(PinConceptsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
