namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>IHostPaths over the file system.</summary>
internal sealed class HostPaths : IHostPaths
{
    /// <inheritdoc />
    public HostPathKind KindOf(string path) =>
        Directory.Exists(path) ? HostPathKind.Directory : Path.Exists(path) ? HostPathKind.File : HostPathKind.Missing;

    /// <inheritdoc />
    public int? Uid() => CgroupControllers.UidFrom(ReadText("/proc/self/status"));

    /// <inheritdoc />
    public string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public string Resolve(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "/";
        for (var i = 0; i < segments.Length; i++)
        {
            var next = current.TrimEnd('/') + "/" + segments[i];
            var info = new DirectoryInfo(next);
            current = i < segments.Length - 1 && info.LinkTarget is not null ? info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? next : next;
        }

        return current;
    }
}
