using Aw.Kernel;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Makes and removes the scratch directory a desktop run keeps its seeded state in.</summary>
public interface IScratchDirectories
{
    /// <summary>Creates a new empty directory below the system temp directory.</summary>
    /// <param name="prefix">The start of its name.</param>
    /// <returns>The directory (absolute), or AW-EVIDENCE-011.</returns>
    public Result<string> Create(string prefix);

    /// <summary>Removes a directory made by <see cref="Create"/> and everything in it; one that is already gone is fine.</summary>
    /// <param name="path">The directory.</param>
    public void Remove(string path);
}
