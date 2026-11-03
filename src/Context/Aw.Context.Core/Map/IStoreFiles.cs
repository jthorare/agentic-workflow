using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>The store's files on disk, read by the 'context:store' doctor row.</summary>
public interface IStoreFiles
{
    /// <summary>The sizes of context.db and context.db-wal.</summary>
    /// <returns>The sizes, null when there is no store file yet, or SND-CONTEXT-004 when a file can't be read.</returns>
    public Result<StoreFileSizes?> Sizes();
}
