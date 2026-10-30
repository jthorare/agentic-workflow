using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.RelinkPinAliases;

/// <summary>The pin-alias relink (concept pins spec §3.4): not built yet.</summary>
internal sealed class RelinkPinAliasesService : IService<RelinkPinAliasesRequest, Core.Linking.RelinkPinAliasesResult>
{
    public ValueTask<Result<Core.Linking.RelinkPinAliasesResult>> Handle(RelinkPinAliasesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Q9: not built yet");
}
