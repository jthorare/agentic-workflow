using Aw.Kernel;

namespace Aw.Sindri.Core.ModelCheck;

/// <summary>Makes and removes the temp directory one model-check run hands TLC as TMPDIR (tlc.sh runs <c language="text">mktemp -d</c> in it, which fails when it is missing).</summary>
public interface IScratchDirectories
{
    /// <summary>Makes a new, empty directory under <paramref name="parent"/>, creating <paramref name="parent"/> when it is missing; two calls never return the same path.</summary>
    /// <param name="parent">The base directory.</param>
    /// <returns>The new directory's absolute path; an error when it cannot be made.</returns>
    public Result<string> Make(string parent);

    /// <summary>Removes <paramref name="directory"/> and everything under it; never throws, a directory that is already gone is fine.</summary>
    /// <param name="directory">A directory <see cref="Make"/> returned.</param>
    public void Remove(string directory);
}
