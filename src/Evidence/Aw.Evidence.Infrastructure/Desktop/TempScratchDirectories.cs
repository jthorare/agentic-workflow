using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Desktop;

/// <summary>Scratch directories below the system temp directory.</summary>
internal sealed class TempScratchDirectories : IScratchDirectories
{
    /// <inheritdoc/>
    public Result<string> Create(string prefix)
    {
        try
        {
            return Directory.CreateTempSubdirectory(prefix).FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EvidenceErrors.FileUnavailable.With($"the scratch directory could not be made: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Remove(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Something still holds a file; the system temp cleaner owns it now. A leftover must not fail a run.
        }
    }
}
