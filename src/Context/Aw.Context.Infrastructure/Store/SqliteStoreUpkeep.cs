using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Store;

/// <summary>The upkeep port over context.db's own non-pooled connection (<see cref="ContextDatabase.Upkeep"/>).</summary>
internal sealed class SqliteStoreUpkeep(ContextDatabase database) : IStoreUpkeep
{
    public Result<StoreUpkeep?> Run() => database.Upkeep();
}
