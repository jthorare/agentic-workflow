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

    /// <summary>
    /// Moves an owner's folder out of the listing (one rename, so a run that starts after it finds no folder and provisions a fresh one) and returns the handle that deletes it or puts it back.
    /// The caller checks the owner's runs again between the two.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The staged purge (<see cref="IStagedPurge.Count"/> 0 when the owner had no folder), or SND-RES-008 naming the folder when it could not be moved.</returns>
    public Result<IStagedPurge> Stage(ResourceOwner owner);

    /// <summary>Lists every entry under the resources root that <see cref="List"/> never shows: a purge's aside folder left behind, or anything else that is not a known kind folder.</summary>
    /// <returns>The leftovers, by name; none when the root holds only kind folders.</returns>
    public Result<IReadOnlyList<ResourceLeftover>> Leftovers();
}
