namespace Aw.Sindri.Core.Reviews;

/// <summary>The library's roots as the page lists them: the <c language="text">aw</c> root, then the vaults in profile order, then the repo roots.</summary>
public static class LibraryRoots
{
    /// <summary>Orders and words the roots.</summary>
    /// <param name="roots">What is known about each root, vaults and repos in the order the profile has them.</param>
    /// <returns>The rows, <see cref="LibraryRootKind"/> first and the given order within a kind.</returns>
    public static IReadOnlyList<LibraryRootRow> Assemble(IEnumerable<LibraryRootFacts> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        return [.. roots.OrderBy(static r => r.Kind).Select(Row)];
    }

    private static LibraryRootRow Row(LibraryRootFacts root) => new(
        root.Name, root.Kind switch { LibraryRootKind.Aw => "aw", LibraryRootKind.Vault => "vault", LibraryRootKind.Repo => "repo", _ => throw new ArgumentOutOfRangeException(nameof(root), root.Kind, "not a root kind") }, root.Path,
        Scope: root is { Kind: LibraryRootKind.Repo, Repo: { } repo } ? repo + " · docs/**" : null,
        On: !root.Off, root.Deny, root.ScannedAt, root.Docs, root.Errors, root.Unreadable);
}
