using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetHomeRow;

/// <summary>One Home row by id (home spec §8). Stub: the red test comes first.</summary>
internal sealed class GetHomeRowService : IService<GetHomeRowRequest, GetHomeRowResponse>
{
    public ValueTask<Result<GetHomeRowResponse>> Handle(GetHomeRowRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
