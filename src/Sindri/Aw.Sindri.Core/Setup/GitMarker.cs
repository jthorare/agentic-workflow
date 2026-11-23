namespace Aw.Sindri.Core.Setup;

/// <summary>What a folder's '.git' entry is (<see cref="IRepoScanTree.Marker"/>).</summary>
public enum GitMarker
{
    /// <summary>No '.git' entry: not the top of a checkout.</summary>
    None,

    /// <summary>A '.git' folder: a main checkout.</summary>
    Folder,

    /// <summary>A '.git' file: a linked worktree (or a submodule), folded into its main checkout.</summary>
    File,
}
