using Aw.Kernel;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>
/// Reads and purges the resources under the resources root. The files are the data, so a listing walks the folders: <c language="text">workflow/&lt;slug&gt;</c>, <c language="text">toolkit/&lt;name&gt;</c>
/// and <c language="text">node/&lt;slug&gt;/&lt;id&gt;</c>, each holding <c language="text">&lt;name&gt;.db</c> files (databases) and folders (disks). Symlinks are never followed or counted.
/// </summary>
/// <param name="root">The resources root, <c language="text">$AW_SINDRI_HOME/resources</c>.</param>
/// <param name="entryCap">Largest number of files and folders one disk measurement visits (tests pass a small one).</param>
internal sealed class ResourceFiles(string root, int entryCap = DiskMeter.FileCap) : IResourceFiles
{
    private const string DatabaseSuffix = ".db";

    internal string Root { get; } = root;

    public Result<IReadOnlyList<ResourceEntry>> List(ResourceOwner? owner)
    {
        var owners = owner is null ? Owners() : Enumerable.Repeat(owner, 1);
        var entries = new List<ResourceEntry>();
        foreach (var each in owners)
        {
            entries.AddRange(Entries(each));
        }

        IReadOnlyList<ResourceEntry> sorted = [.. entries.OrderBy(static e => e.Owner.ToString(), StringComparer.Ordinal).ThenBy(static e => e.Kind).ThenBy(static e => e.Name, StringComparer.Ordinal)];
        return Results.Success(sorted);
    }

    public Result<int> Purge(ResourceOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var folder = Folder(owner);
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        var count = Entries(owner).Count();

        // why: Directory.Delete(recursive) removes a symlink inside the folder as a link and never follows it
        Directory.Delete(folder, recursive: true);
        return count;
    }

    public Result<IStagedPurge> Stage(ResourceOwner owner) => throw new NotSupportedException();

    private string Folder(ResourceOwner owner) => Path.GetFullPath(Path.Combine(Root, owner.Folder));

    private IEnumerable<ResourceOwner> Owners()
    {
        foreach (var kind in new[] { "workflow", "toolkit" })
        {
            foreach (var dir in Subfolders(Path.Combine(Root, kind)))
            {
                if (ResourceOwner.Parse($"{kind}:{Path.GetFileName(dir)}") is { IsSuccess: true } parsed)
                {
                    yield return parsed.Value;
                }
            }
        }

        foreach (var slug in Subfolders(Path.Combine(Root, "node")))
        {
            foreach (var dir in Subfolders(slug))
            {
                if (ResourceOwner.Parse($"node:{Path.GetFileName(slug)}/{Path.GetFileName(dir)}") is { IsSuccess: true } parsed)
                {
                    yield return parsed.Value;
                }
            }
        }
    }

    private IEnumerable<ResourceEntry> Entries(ResourceOwner owner)
    {
        var folder = Folder(owner);
        if (!Directory.Exists(folder))
        {
            yield break;
        }

        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null)
            {
                continue;
            }

            if (entry is DirectoryInfo dir)
            {
                var used = DiskMeter.Check(dir.Name, dir.FullName, long.MaxValue, entryCap);
                yield return new ResourceEntry(owner, ResourceKind.Disk, dir.Name, used.IsSuccess ? used.Value : 0, dir.FullName);
            }
            else if (entry is FileInfo file && file.Name.EndsWith(DatabaseSuffix, StringComparison.Ordinal))
            {
                yield return new ResourceEntry(owner, ResourceKind.Database, file.Name[..^DatabaseSuffix.Length], file.Length, file.FullName);
            }
        }
    }

    private static IEnumerable<string> Subfolders(string folder) =>
        Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateDirectories().Where(static d => d.LinkTarget is null).Select(static d => d.FullName) : [];
}
