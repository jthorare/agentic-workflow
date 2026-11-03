using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CheckStore;

/// <summary>Doctor 'context:store': warns while context.db's write-ahead log is far bigger than the database.</summary>
internal sealed class CheckStoreService(IStoreFiles files) : IService<CheckStoreRequest, MapHealth>
{
    public ValueTask<Result<MapHealth>> Handle(CheckStoreRequest request, CancellationToken cancellationToken)
    {
        var sizes = files.Sizes();
        Result<MapHealth> health = sizes.IsError
            ? new MapHealth(StoreHealth.Name, MapHealthState.Fail, $"{sizes.Error.Code}: {sizes.Error.Message}", "check $AW_SINDRI_HOME/context permissions")
            : StoreHealth.Check(sizes.Value);
        return ValueTask.FromResult(health);
    }
}
