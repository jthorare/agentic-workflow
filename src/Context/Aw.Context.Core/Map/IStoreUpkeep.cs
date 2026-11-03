using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>Keeps context.db healthy: refreshes the planner statistics and checkpoints the write-ahead log, never waiting on a reader or writer.</summary>
public interface IStoreUpkeep
{
    /// <summary>Runs one pass.</summary>
    /// <returns>What it did, null when there is no store file yet, or SND-CONTEXT-004 when the file can't be maintained.</returns>
    public Result<StoreUpkeep?> Run();
}
