using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListHome;

/// <summary>A page of Home rows (home spec §8). Stub: the red test comes first.</summary>
internal sealed class ListHomeService : IService<ListHomeRequest, ListHomeResponse>
{
    public ValueTask<Result<ListHomeResponse>> Handle(ListHomeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
