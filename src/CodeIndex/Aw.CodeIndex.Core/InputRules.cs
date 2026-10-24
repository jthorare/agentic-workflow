using System.Globalization;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Planning;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Core;

/// <summary>
/// What the snapshot keeps of a commit's tree (spec §5.1, §9): regular files only, with a safe path, outside the deny globs,
/// under the file size cap (the total cap counts code files only). Pure, so the git adapter and the fake apply the same rules.
/// </summary>
public static class InputRules
{
    /// <summary>The secret globs every repo has; <see cref="SnapshotLimits.DenyPaths"/> only adds to them.</summary>
    public static IReadOnlyList<string> SecretGlobs { get; } =
    [
        "**/.env", "**/.env.*", "**/*.pem", "**/*.key", "**/*.p12", "**/*.pfx", "**/id_rsa*", "**/id_dsa*", "**/id_ecdsa*", "**/id_ed25519*",
        "**/*.tfstate", "**/*.tfvars", "**/credentials.json", "**/.npmrc", "**/.netrc", "**/secrets/**",
    ];

    /// <summary>Whether every component of a path is safe to write under a snapshot root.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>False for an empty, '..' or '.git' (any case) component, a backslash, a NUL, or an absolute path.</returns>
    public static bool IsSafePath(string path)
    {
        if (path.Length == 0 || path.Contains('\\', StringComparison.Ordinal) || path.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var component in path.Split('/'))
        {
            if (component.Length == 0 || component == ".." || string.Equals(component, ".git", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether a path matches a built-in secret glob or one of the added ones, ignoring case.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="addedGlobs">The profile's and the repo's globs.</param>
    /// <returns>True when the path is denied.</returns>
    public static bool IsDenied(string path, IReadOnlyList<string> addedGlobs) => MatchesAny(path, SecretGlobs.Concat(addedGlobs));

    /// <summary>Whether a path matches any of the globs, ignoring case ('*' and '?' inside a segment, '**' across segments).</summary>
    /// <param name="path">The repo-relative path, with '/' separators.</param>
    /// <param name="globs">The globs; an empty one matches nothing.</param>
    /// <returns>True when one matches.</returns>
    public static bool MatchesAny(string path, IEnumerable<string> globs)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(globs);
        var segments = path.Split('/');
        return globs.Any(glob => glob.Length > 0 && Matches(glob.Split('/'), 0, segments, 0));
    }

    /// <summary>Applies the rules to a commit's tree.</summary>
    /// <param name="commit">The commit the tree belongs to.</param>
    /// <param name="entries">Every blob and submodule entry, as git lists them.</param>
    /// <param name="limits">The deny globs and caps.</param>
    /// <returns>The kept files in path order and a count per skip reason; SND-CODEIDX-003 when the kept code files' sizes pass the cap.</returns>
    public static Result<SnapshotInventory> Apply(CommitSha commit, IEnumerable<TreeEntry> entries, SnapshotLimits limits)
    {
        var skipped = new Dictionary<SkipReason, int>();
        var kept = new List<SnapshotFile>();
        long total = 0;
        foreach (var entry in entries)
        {
            if (SkipReasonOf(entry, limits) is { } reason)
            {
                skipped[reason] = skipped.GetValueOrDefault(reason) + 1;
                continue;
            }

            var size = entry.Size ?? 0;
            kept.Add(new SnapshotFile(entry.Path, entry.Blob, size));
            if (LanguageTable.Of(entry.Path) is not null)
            {
                total += size;
            }
        }

        if (total > limits.MaxTotalBytes)
        {
            return CodeIndexErrors.InputTooLarge.With(string.Create(CultureInfo.InvariantCulture, $"{total} kept code bytes are over the cap of {limits.MaxTotalBytes}"));
        }

        kept.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return new SnapshotInventory(commit, kept, skipped);
    }

    private static SkipReason? SkipReasonOf(TreeEntry entry, SnapshotLimits limits)
    {
        if (entry.Mode == "120000")
        {
            return SkipReason.Symlink;
        }

        if (entry.Mode == "160000")
        {
            return SkipReason.Submodule;
        }

        if ((entry.Mode != "100644" && entry.Mode != "100755") || !IsSafePath(entry.Path))
        {
            return SkipReason.UnsafePath;
        }

        if (IsDenied(entry.Path, limits.DenyPaths))
        {
            return SkipReason.Denied;
        }

        if (entry.Size is not { } size)
        {
            return SkipReason.Missing;
        }

        return size > limits.MaxFileBytes ? SkipReason.TooLarge : null;
    }

    private static bool Matches(string[] glob, int g, string[] path, int p)
    {
        if (g == glob.Length)
        {
            return p == path.Length;
        }

        if (glob[g] == "**")
        {
            if (g == glob.Length - 1)
            {
                return true;
            }

            for (var skip = p; skip <= path.Length; skip++)
            {
                if (Matches(glob, g + 1, path, skip))
                {
                    return true;
                }
            }

            return false;
        }

        return p < path.Length && SegmentMatches(glob[g], path[p]) && Matches(glob, g + 1, path, p + 1);
    }

    private static bool SegmentMatches(string pattern, string text)
    {
        int pi = 0, ti = 0, star = -1, mark = 0;
        while (ti < text.Length)
        {
            if (pi < pattern.Length && pattern[pi] == '*')
            {
                star = pi++;
                mark = ti;
            }
            else if (pi < pattern.Length && (pattern[pi] == '?' || char.ToUpperInvariant(pattern[pi]) == char.ToUpperInvariant(text[ti])))
            {
                pi++;
                ti++;
            }
            else if (star >= 0)
            {
                pi = star + 1;
                ti = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (pi < pattern.Length && pattern[pi] == '*')
        {
            pi++;
        }

        return pi == pattern.Length;
    }
}
