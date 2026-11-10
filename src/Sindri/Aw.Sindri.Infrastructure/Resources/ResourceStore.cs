using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>Creates an owner's resource folders and files under the resources root: directories 0700, files 0600, no symlinks.</summary>
/// <param name="root">The resources root, <c language="text">$AW_SINDRI_HOME/resources</c>.</param>
internal sealed class ResourceStore(string root)
{
    private const UnixFileMode FolderMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode DatabaseMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Gets the resources root.</summary>
    internal string Root { get; } = root;

    /// <summary>Creates what <paramref name="plan"/> lists. A second call changes nothing but modes: every folder ends 0700 and every database file 0600.</summary>
    /// <param name="owner">The owner; every path must sit under its folder.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>Success, SND-RES-004 for a path outside the owner's folder or a symlink on the way, or SND-RES-006 for a path that exists as the wrong type.</returns>
    internal Result<Unit> Provision(ResourceOwner owner, ResourcePlan plan)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(plan);
        var home = Path.GetFullPath(Path.Combine(Root, owner.Folder));
        foreach (var item in plan.Create)
        {
            var full = Path.GetFullPath(item.Path);
            if (!string.Equals(Path.GetDirectoryName(full), home, StringComparison.Ordinal))
            {
                return SindriErrors.ResourcePathEscapes.With($"The {What(item)} '{item.Name}' would be created outside the folder of {owner}.");
            }
        }

        if (EnsureChain(Root, home) is { } chain)
        {
            return chain;
        }

        foreach (var item in plan.Create)
        {
            var made = item.Kind == ResourceKind.Disk ? EnsureFolder(item.Path) : EnsureDatabase(item.Path);
            if (made is not null)
            {
                return made;
            }
        }

        return Unit.Value;
    }

    private static string What(PlannedResource item) => item.Kind.ToString().ToLowerInvariant();

    /// <summary>Makes <paramref name="root"/> and each folder down to <paramref name="leaf"/> an owner-only folder.</summary>
    private static Error? EnsureChain(string root, string leaf)
    {
        var current = Path.GetFullPath(root);
        if (EnsureFolder(current) is { } bad)
        {
            return bad;
        }

        foreach (var part in Path.GetRelativePath(current, leaf).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            if (EnsureFolder(current) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }

    // why: CreateDirectory with a mode leaves no window at the umask's 0755, and an existing folder made too open by hand is tightened
    private static Error? EnsureFolder(string path)
    {
        if (Kind(path) is { } wrong and not FileKind.Folder)
        {
            return Refuse(path, wrong, "a folder");
        }

        if (!OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(path, FolderMode);
            File.SetUnixFileMode(path, FolderMode);
        }
        else
        {
            _ = Directory.CreateDirectory(path);
        }

        return null;
    }

    private static Error? EnsureDatabase(string path)
    {
        if (Kind(path) is { } wrong and not FileKind.File)
        {
            return Refuse(path, wrong, "a file");
        }

        SqliteFileProvisioner.Create(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, DatabaseMode);   // an existing file keeps its rows, not a mode someone widened
        }

        return null;
    }

    private static Error Refuse(string path, FileKind wrong, string want) => wrong == FileKind.Link
        ? SindriErrors.ResourcePathEscapes.With($"{path} is a symlink, which could point out of the resources folder. Remove it and provision again.")
        : SindriErrors.ResourceInvalid.With($"{path} exists but isn't {want}. Move it away and provision again.");

    /// <summary>What sits at <paramref name="path"/> without following a symlink, or null when nothing does.</summary>
    private static FileKind? Kind(string path)
    {
        var info = new FileInfo(path);
        if (info.LinkTarget is not null)
        {
            return FileKind.Link;
        }

        return info.Exists ? FileKind.File : Directory.Exists(path) ? FileKind.Folder : null;
    }

    private enum FileKind
    {
        File,
        Folder,
        Link,
    }
}
