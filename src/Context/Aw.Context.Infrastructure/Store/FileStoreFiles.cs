using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Store;

/// <summary>context.db and context.db-wal, sized with FileInfo.</summary>
internal sealed class FileStoreFiles(ContextStoreSettings settings) : IStoreFiles
{
    public Result<StoreFileSizes?> Sizes()
    {
        _ = settings;
        throw new NotSupportedException();
    }
}
