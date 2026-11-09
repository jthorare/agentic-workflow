using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Infrastructure.Scoping;

/// <summary>
/// The brief file 'read-brief' reads (scoping spec §8.2, SND-SCOPING-004): a path inside a root, never under a denied glob (<see cref="BriefPaths"/>), and never through
/// a symbolic link that leaves the root.
/// </summary>
internal sealed class BriefFiles : IBriefFiles
{
    /// <inheritdoc/>
    public async ValueTask<Result<BriefFile>> Read(string path, IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count == 0)
        {
            return SindriErrors.InvalidInput.With("read-brief needs a directory the brief's path is inside (fix: map the item to a repo of the profile, or pass --repo-root <dir>)");
        }

        if (Resolve(path, roots) is not { } resolved)
        {
            return ScopingErrors.BriefPathRefused.With($"the brief path {path} is refused: it is outside the repo root and the notes dir, or under a denied glob (fix: name a file inside the repo root or the notes dir that is not a secret)");
        }

        if (!File.Exists(resolved))
        {
            return SindriErrors.InvalidInput.With($"the brief {path} does not exist under {string.Join(" or ", roots)} (fix: name a file that exists)");
        }

        if (new FileInfo(resolved).Length > BriefPaths.MaxBytes)
        {
            return SindriErrors.InvalidInput.With($"the brief {path} is over {BriefPaths.MaxBytes} bytes (fix: name one section with \"section\", or a smaller file)");
        }

        var text = await File.ReadAllTextAsync(resolved, cancellationToken).ConfigureAwait(false);
        return new BriefFile(path, text, "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12]);
    }

    /// <summary>The file's full path when its real location (every directory and the file itself followed through links) is inside the root's real location and not denied; null otherwise.</summary>
    private static string? Resolve(string path, IReadOnlyList<string> roots)
    {
        foreach (var root in roots.Select(static r => Path.GetFullPath(r)))
        {
            var candidate = Path.GetFullPath(Path.Combine(root, path));
            if (Inside(root, candidate) && Allowed(root, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>True when the candidate's real location is inside the root's real location and not denied; a link loop is refused like a link that leaves the root.</summary>
    private static bool Allowed(string root, string candidate)
    {
        try
        {
            var realRoot = Real(root);
            var real = Real(candidate);
            return Inside(realRoot, real) && !BriefPaths.Denied(Path.GetRelativePath(root, candidate)) && !BriefPaths.Denied(Path.GetRelativePath(realRoot, real));
        }
        catch (IOException)
        {
            return false;   // boundary: a circular symbolic link has no final target
        }
    }

    /// <summary>The path with every existing component followed through its link, so a symlinked directory counts as where it points.</summary>
    private static string Real(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full) ?? string.Empty;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && new FileInfo(current).ResolveLinkTarget(returnFinalTarget: true) is { } target)
            {
                current = Real(target.FullName);
            }
        }

        return current;
    }

    private static bool Inside(string root, string path) => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
