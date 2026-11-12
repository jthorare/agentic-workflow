using Aw.Kernel;

namespace Aw.Sindri.Core.Resources;

/// <summary>An owner's folder moved aside for a purge: delete it for good, or put it back.</summary>
public interface IStagedPurge
{
    /// <summary>Gets how many resources the folder holds (databases, disks and stray files).</summary>
    public int Count { get; }

    /// <summary>Deletes the moved folder.</summary>
    /// <returns>The count removed, or SND-RES-008 naming the folder when it could not be deleted.</returns>
    public Result<int> Delete();

    /// <summary>Puts the folder back where it was.</summary>
    /// <returns>The count restored, or SND-RES-008 naming where the folder now is.</returns>
    public Result<int> Restore();
}
