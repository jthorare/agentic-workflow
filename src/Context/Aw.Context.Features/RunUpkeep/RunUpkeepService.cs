using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.RunUpkeep;

/// <summary>The 'context-upkeep' service's pass: refreshes context.db's planner statistics and checkpoints its log, never waiting on a reader.</summary>
internal sealed class RunUpkeepService(IStoreUpkeep upkeep) : IService<RunUpkeepRequest, StoreUpkeep?>
{
    public ValueTask<Result<StoreUpkeep?>> Handle(RunUpkeepRequest request, CancellationToken cancellationToken)
    {
        _ = upkeep;
        throw new NotSupportedException();
    }
}
