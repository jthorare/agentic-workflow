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

    private static readonly string[] _buildFolders = ["node_modules", "bin", "obj", "target"];

    /// <summary>Whether the walk skips the folder named <paramref name="name"/>: a dot folder, 'node_modules', 'bin', 'obj' or 'target'.</summary>
    /// <param name="name">The folder's name (not its path).</param>
    /// <returns>True when the walk never enters it.</returns>
    public static bool Skips(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.StartsWith('.') || Array.Exists(_buildFolders, folder => string.Equals(folder, name, StringComparison.Ordinal));
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="home"/> or a folder under it.</summary>
    /// <param name="path">A real path.</param>
    /// <param name="home">The home folder's real path.</param>
    /// <returns>True when the scan may start there.</returns>
    public static bool InsideHome(string path, string home)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(home);
        var at = path.TrimEnd('/');
        var top = home.TrimEnd('/');
        return top.Length > 0 && (string.Equals(at, top, StringComparison.Ordinal) || at.StartsWith(top + "/", StringComparison.Ordinal));
    }

    /// <summary>The host and 'owner/name' slug of a remote URL (https, ssh or scp-like 'git@host:owner/name'), without the user name, password or '.git'.</summary>
    /// <param name="url">The remote URL, or null.</param>
    /// <returns>The host and slug, or null when the URL has no host and two-part path.</returns>
    public static (string Host, string Slug)? Origin(string? url)
    {
        if (Split(url?.Trim()) is not { } split)
        {
            return null;
        }

        var parts = split.Path.Trim('/').Split('/');
        if (parts.Length != 2 || parts[0].Length == 0)
        {
            return null;
        }

        var name = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        return name.Length == 0 ? null : (split.Host.ToLowerInvariant(), parts[0] + "/" + name);
    }

    // why: the user name and password (a token) sit before the '@'; only the host after it and the path are kept, so the secret never leaves this method
    private static (string Host, string Path)? Split(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (url.Contains("://", StringComparison.Ordinal))
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile && uri.Host.Length > 0 ? (uri.Host, Uri.UnescapeDataString(uri.AbsolutePath)) : null;
        }

        var colon = url.IndexOf(':', StringComparison.Ordinal);   // scp-like: [user@]host:owner/name
        if (colon <= 0)
        {
            return null;
        }

        var authority = url[..colon];
        var host = authority[(authority.LastIndexOf('@') + 1)..];
        return host.Length == 0 || host.Contains('/', StringComparison.Ordinal) || host.Contains(' ', StringComparison.Ordinal) ? null : (host, url[(colon + 1)..]);
    }
}
