using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The pure rules of adding a repo (setup-once spec §5.1): the name a folder gets by default, the job key of a resolved path, and the folders that can never be a repo (the file system's root, the
/// user's home, the Sindri home and anything around or inside it).
/// </summary>
public static partial class RepoFolders
{
    private const int NameMax = 100;
    private const string FallbackName = "repo";

    /// <summary>The default repo name for <paramref name="path"/>: the folder's name in lowercase, each run of characters outside '[a-z0-9]' one '-', no '-' at either end, at most 100 characters.</summary>
    /// <param name="path">The folder's path.</param>
    /// <returns>A valid repo name ('repo' when the folder name has no letter or digit).</returns>
    public static string NameOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var folder = path.TrimEnd('/', '\\');
        var last = folder.LastIndexOfAny(['/', '\\']);
        var name = Other.Replace((last < 0 ? folder : folder[(last + 1)..]).ToLowerInvariant(), "-").Trim('-');
        if (name.Length > NameMax)
        {
            name = name[..NameMax].TrimEnd('-');
        }

        return name.Length == 0 ? FallbackName : name;
    }

    /// <summary>The key of an AddRepo job for the resolved <paramref name="resolvedPath"/>: the first 16 hex characters of its SHA-256, so the path is never in a job event.</summary>
    /// <param name="resolvedPath">The real path of the main checkout.</param>
    /// <returns>The key.</returns>
    public static JobKey KeyOf(string resolvedPath)
    {
        ArgumentNullException.ThrowIfNull(resolvedPath);
        return JobKey.From(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(resolvedPath)))[..16]);
    }

    /// <summary>Why <paramref name="resolvedPath"/> can never be a repo, or null: it is '/', the user's home, a parent of the Sindri home, or inside it.</summary>
    /// <param name="resolvedPath">The real path of the folder.</param>
    /// <param name="userHome">The user's home folder, real.</param>
    /// <param name="sindriHome">The Sindri home ($AW_SINDRI_HOME), real.</param>
    /// <returns>The reason in plain words, or null when the folder may be added.</returns>
    public static string? Refuse(string resolvedPath, string userHome, string sindriHome)
    {
        ArgumentNullException.ThrowIfNull(resolvedPath);
        ArgumentNullException.ThrowIfNull(userHome);
        ArgumentNullException.ThrowIfNull(sindriHome);
        var path = Trimmed(resolvedPath);
        if (path.Length == 0)
        {
            return "that is the top of the file system, not a repo";
        }

        if (string.Equals(path, Trimmed(userHome), StringComparison.Ordinal))
        {
            return "that is your home folder, not a repo";
        }

        var home = Trimmed(sindriHome);
        if (string.Equals(path, home, StringComparison.Ordinal) || path.StartsWith(home + "/", StringComparison.Ordinal))
        {
            return "that is inside Sindri's own folder";
        }

        return home.StartsWith(path + "/", StringComparison.Ordinal) ? "that folder contains Sindri's own folder" : null;
    }

    private static string Trimmed(string path) => path.TrimEnd('/');

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Other { get; }
}
