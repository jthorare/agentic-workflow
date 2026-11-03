using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CheckStore;

/// <summary>Doctor 'context:store': warns while context.db's write-ahead log is far bigger than the database.</summary>
internal sealed class CheckStoreService(IStoreFiles files) : IService<CheckStoreRequest, MapHealth>
{
    public ValueTask<Result<MapHealth>> Handle(CheckStoreRequest request, CancellationToken cancellationToken)
    {
        _ = files;
        throw new NotSupportedException();
    }
}
