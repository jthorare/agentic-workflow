namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The pure rules of the repo scan (first-run spec §3.3): the folders the walk skips, the cap and the depth, the home-folder bound of the root, and the origin URL cut to its host and
/// 'owner/name' slug with the user name and password dropped.
/// </summary>
public static class RepoScanRules
{
    /// <summary>The most repos one scan lists.</summary>
    public const int Cap = 500;

    /// <summary>How deep the walk goes below the root when the caller names no depth.</summary>
    public const int DefaultDepth = 3;

    /// <summary>The deepest walk a caller may ask for.</summary>
    public const int MaxDepth = 6;

    /// <summary>Whether the walk skips the folder named <paramref name="name"/>: a dot folder, 'node_modules', 'bin', 'obj' or 'target'.</summary>
    /// <param name="name">The folder's name (not its path).</param>
    /// <returns>True when the walk never enters it.</returns>
    public static bool Skips(string name) => throw new NotSupportedException("RepoScanRules.Skips is not built yet");

    /// <summary>Whether <paramref name="path"/> is <paramref name="home"/> or a folder under it.</summary>
    /// <param name="path">A real path.</param>
    /// <param name="home">The home folder's real path.</param>
    /// <returns>True when the scan may start there.</returns>
    public static bool InsideHome(string path, string home) => throw new NotSupportedException("RepoScanRules.InsideHome is not built yet");

    /// <summary>The host and 'owner/name' slug of a remote URL (https, ssh or scp-like 'git@host:owner/name'), without the user name, password or '.git'.</summary>
    /// <param name="url">The remote URL, or null.</param>
    /// <returns>The host and slug, or null when the URL has no host and two-part path.</returns>
    public static (string Host, string Slug)? Origin(string? url) => throw new NotSupportedException("RepoScanRules.Origin is not built yet");
}
