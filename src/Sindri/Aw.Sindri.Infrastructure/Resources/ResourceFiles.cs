using Aw.Kernel;
using Aw.Sindri.Core;
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
        var staged = Stage(owner);
        return staged.IsError ? staged.Error : staged.Value.Delete();
    }

    public Result<IStagedPurge> Stage(ResourceOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var folder = Folder(owner);
        if (!Directory.Exists(folder))
        {
            return Results.Success<IStagedPurge>(new StagedFolder(owner, folder, aside: null, count: 0));
        }

        var count = new DirectoryInfo(folder).EnumerateFileSystemInfos().Count(static e => e.LinkTarget is null);
        var aside = Path.Combine(Root, $".purging-{Guid.NewGuid():N}");
        try
        {
            // why: one rename takes the folder out of the listing; a run that starts after it provisions a fresh folder at the old path
            Directory.Move(folder, aside);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ResourcePurgeFailed.With($"Could not move '{owner.Folder}' aside to purge it: {e.Message}");
        }

        return Results.Success<IStagedPurge>(new StagedFolder(owner, folder, aside, count));
    }

    public Result<IReadOnlyList<ResourceLeftover>> Leftovers() => throw new NotSupportedException();

    private sealed class StagedFolder(ResourceOwner owner, string folder, string? aside, int count) : IStagedPurge
    {
        public int Count { get; } = count;

        public Result<int> Delete()
        {
            if (aside is null)
            {
                return 0;
            }

            try
            {
                // why: Directory.Delete(recursive) removes a symlink inside the folder as a link and never follows it
                Directory.Delete(aside, recursive: true);
                return Count;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                var put = Restore();
                var where = put.IsError ? $"; what is left is at '{aside}'" : string.Empty;
                return SindriErrors.ResourcePurgeFailed.With($"Could not delete '{owner.Folder}': {e.Message}{where}");
            }
        }

        public Result<int> Restore()
        {
            if (aside is null)
            {
                return 0;
            }

            try
            {
                Directory.Move(aside, folder);
                return Count;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return SindriErrors.ResourcePurgeFailed.With($"Could not put '{owner.Folder}' back; it is at '{aside}': {e.Message}");
            }
        }
    }

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
                yield return new ResourceEntry(owner, ResourceKind.Disk, dir.Name, used.IsSuccess ? used.Value : 0, dir.FullName, unmeasured: used.IsError);
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
