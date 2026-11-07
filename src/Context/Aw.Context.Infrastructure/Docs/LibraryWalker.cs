using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>
/// Lists the markdown files a library root may hold. A symlink (file or folder) and any dot folder is skipped; the 'aw' root is opened only at
/// <c language="text">&lt;slug&gt;/plans/**</c>, <c language="text">&lt;slug&gt;/proof/*/</c> and <c language="text">&lt;slug&gt;/system-design/*/</c>, never at the tool state beside them.
/// A folder that can't be listed fails the whole root, so a partial listing never reads as "those files are gone".
/// </summary>
internal static class LibraryWalker
{
    private static readonly Glob _awFiles = new(["*/plans/**/*.md", "*/proof/*/*.md", "*/system-design/*/*.md"]);
    private static readonly string[] _awFolders = ["plans", "proof", "system-design"];
    private static readonly string[] _toolState = ["cache", "skill-evals", "digests", "meta", "logs"];

    internal static LibraryListing List(LibraryRoot root)
    {
        if (!Directory.Exists(root.Path))
        {
            return Failed($"{root.Path} is not a folder that exists");
        }

        var files = new List<LibraryFile>();
        var deny = new LibraryDeny(root.Deny);
        try
        {
            Walk(new DirectoryInfo(root.Path), relative: "", root.IsAw, deny, files);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: an unmounted drive or a folder the owner can't read
        {
            return Failed($"{root.Path} can't be read: {e.Message}");
        }
        catch (RegexMatchTimeoutException)   // boundary: a deny glob that backtracks without end on a deep path
        {
            return Failed($"{root.Path} can't be read: a deny glob takes too long to match a path (simplify the vault's deny globs)");
        }

        files.Sort(static (a, b) => string.CompareOrdinal(a.Relative, b.Relative));
        return new LibraryListing(files, Problem: null);
    }

    private static LibraryListing Failed(string problem) => new([], problem);

    private static void Walk(DirectoryInfo folder, string relative, bool awRoot, LibraryDeny deny, List<LibraryFile> files)
    {
        var packet = PacketIn(folder);
        foreach (var entry in folder.EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null || entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;   // a symlink is never followed
            }

            var path = relative.Length == 0 ? entry.Name : relative + "/" + entry.Name;
            if (entry is DirectoryInfo directory)
            {
                if (Opens(directory.Name, path, awRoot))
                {
                    Walk(directory, path, awRoot, deny, files);
                }
            }
            else if (Wanted((FileInfo)entry, path, awRoot, deny))
            {
                files.Add(new LibraryFile(path, entry.FullName, ((FileInfo)entry).Length, Millisecond(entry.LastWriteTimeUtc), packet));
            }
        }
    }

    // The source of a generated doc, beside it; a symlink is never followed.
    private static LibraryPacket? PacketIn(DirectoryInfo folder)
    {
        var file = new FileInfo(Path.Combine(folder.FullName, GeneratedSource.FileName));
        return file.Exists && file.LinkTarget is null && !file.Attributes.HasFlag(FileAttributes.ReparsePoint)
            ? new LibraryPacket(file.FullName, file.Length, Millisecond(file.LastWriteTimeUtc))
            : null;
    }

    private static bool Wanted(FileInfo file, string path, bool awRoot, LibraryDeny deny) =>
        file.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && !deny.Denies(path) && (!awRoot || _awFiles.Matches(path));

    private static bool Opens(string name, string path, bool awRoot) =>
        !name.StartsWith('.') && (!awRoot || OpensInAw(name, path.Split('/')));

    // <slug>/{plans,proof,system-design}: plans holds any depth, proof and system-design one folder of docs
    private static bool OpensInAw(string name, string[] segments) =>
        segments.Length switch
        {
            1 => !name.StartsWith("bin", StringComparison.Ordinal) && !_toolState.Contains(name, StringComparer.Ordinal),
            2 => _awFolders.Contains(name, StringComparer.Ordinal),
            _ => segments[1] == "plans" || segments.Length <= 3,
        };

    private static DateTimeOffset Millisecond(DateTime utc) => DateTimeOffset.FromUnixTimeMilliseconds(new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds());
}
