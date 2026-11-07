using Aw.Context.Core.Engine;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>Where the library's folders are, as the PR review page shows them: the absolute path with the home folder written <c language="text">~</c>.</summary>
internal sealed class LibraryFolders
{
    private readonly Dictionary<string, string> _shown;

    private LibraryFolders(Dictionary<string, string> shown)
    {
        _shown = shown;
    }

    /// <summary>Reads the folders of the checked library settings.</summary>
    /// <param name="library">The settings, or null when the profile names no library.</param>
    /// <param name="host">The machine's home folders.</param>
    /// <returns>The folders; none when the settings don't resolve.</returns>
    internal static LibraryFolders Of(LibrarySettings? library, LibraryHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var shown = new Dictionary<string, string>(StringComparer.Ordinal);
        var roots = library is null ? null : LibraryPaths.Resolve(library, host);
        if (roots is { IsError: false } resolved)
        {
            foreach (var root in resolved.Value)
            {
                shown[root.Name] = Fold(root.Path, host.Home);
            }
        }

        return new LibraryFolders(shown);
    }

    /// <summary>Names a root's folder.</summary>
    /// <param name="root">The root: <c language="text">aw</c> or a vault's name.</param>
    /// <returns>The folder; the root's own name when it is unknown.</returns>
    internal string Root(string root) => _shown.GetValueOrDefault(root, root);

    /// <summary>Names a doc's file from its record key (<c language="text">doc:&lt;root&gt;/&lt;path in the root&gt;</c>).</summary>
    /// <param name="key">The record key.</param>
    /// <returns>The file; the key as it is when it isn't a doc key.</returns>
    internal string File(string key)
    {
        const string Prefix = "doc:";
        if (!key.StartsWith(Prefix, StringComparison.Ordinal) || key.IndexOf('/', Prefix.Length) is not (> 0 and var slash))
        {
            return key;
        }

        return _shown.TryGetValue(key[Prefix.Length..slash], out var folder) ? folder + key[slash..] : key;
    }

    private static string Fold(string path, string home)
    {
        var trimmed = home.TrimEnd('/');
        return trimmed.Length > 0 && path.StartsWith(trimmed + "/", StringComparison.Ordinal) ? "~" + path[trimmed.Length..] : path;
    }
}
