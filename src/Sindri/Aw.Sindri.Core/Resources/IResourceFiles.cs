using Aw.Kernel;

namespace Aw.Sindri.Core.Resources;

/// <summary>The provisioned resources on disk, under <c lang="text">$AW_SINDRI_HOME/resources</c>. The files are the data; nothing else touches an owner's folder.</summary>
public interface IResourceFiles
{
    /// <summary>Lists what is on disk.</summary>
    /// <param name="owner">One owner, or null for every owner.</param>
    /// <returns>The resources, by owner, kind and name; none when nothing was provisioned.</returns>
    public Result<IReadOnlyList<ResourceEntry>> List(ResourceOwner? owner);

    /// <summary>Deletes an owner's folder with everything in it. Symlinks inside are removed, never followed.</summary>
    /// <param name="owner">The owner.</param>
    /// <returns>How many resources were removed (0 when the owner had none).</returns>
    public Result<int> Purge(ResourceOwner owner);
}
