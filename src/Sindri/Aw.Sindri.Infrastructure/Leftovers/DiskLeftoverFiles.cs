using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Leftovers;

namespace Aw.Sindri.Infrastructure.Leftovers;

/// <summary>ILeftoverFiles over the file system.</summary>
internal sealed class DiskLeftoverFiles : ILeftoverFiles
{
    public string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public Result<Unit> DeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            return Results.Ok();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.IndexOnyxLeftover.With($"{path} could not be deleted ({e.GetType().Name}) (fix: remove it by hand, then run `sindri onyx down --purge` again)");
        }
    }
}
