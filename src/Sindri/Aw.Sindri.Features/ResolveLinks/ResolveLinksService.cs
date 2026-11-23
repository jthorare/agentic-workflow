using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ResolveLinks;

/// <summary>A stub until M1g (messages spec §14): links return 'none' before it.</summary>
internal sealed class ResolveLinksService : IService<ResolveLinksRequest, ResolveLinksResponse>
{
    public ValueTask<Result<ResolveLinksResponse>> Handle(ResolveLinksRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ResolveLinks is not built yet: messages spec §14 phase M1g");
}
