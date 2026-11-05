using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Infrastructure.ModelCheck;

/// <summary><see cref="IScratchDirectories"/> on disk: a random name under the base, so runs at once never share one.</summary>
internal sealed class DiskScratchDirectories : IScratchDirectories
{
    public Result<string> Make(string parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        try
        {
            var directory = Path.Combine(parent, "run-" + Path.GetRandomFileName());
            _ = Directory.CreateDirectory(directory);
            return directory;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.InvalidInput.With($"the model check's temp directory under {parent} cannot be made: {e.Message}");   // boundary: disk
        }
    }

    public void Remove(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // best effort: a leftover temp directory must not turn a finished check into a failure
        }
    }
}
