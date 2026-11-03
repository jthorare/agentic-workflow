using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Store;

/// <summary>context.db and context.db-wal, sized with FileInfo.</summary>
internal sealed class FileStoreFiles(ContextStoreSettings settings) : IStoreFiles
{
    public Result<StoreFileSizes?> Sizes()
    {
        try
        {
            var database = new FileInfo(settings.DatabasePath);
            if (!database.Exists)
            {
                return Results.Success<StoreFileSizes?>(value: null);
            }

            var wal = new FileInfo(settings.DatabasePath + "-wal");
            return Results.Success<StoreFileSizes?>(new StoreFileSizes(database.Length, WalBytes: wal.Exists ? wal.Length : 0));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.StoreUnavailable.With(e.Message);
        }
    }
}
