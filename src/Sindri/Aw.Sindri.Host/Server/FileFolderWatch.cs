namespace Aw.Sindri.Host.Server;

/// <summary>The folder watch over <see cref="FileSystemWatcher"/>: non-recursive, one file name, every kind of change.</summary>
internal sealed class FileFolderWatch : IFolderWatch
{
    public IDisposable Watch(string folder, string fileName, Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        try
        {
            var watcher = new FileSystemWatcher(folder, fileName)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            };
            watcher.Changed += (_, _) => changed();
            watcher.Created += (_, _) => changed();
            watcher.Deleted += (_, _) => changed();
            watcher.Renamed += (_, _) => changed();
            watcher.Error += (_, _) => changed();   // a lost buffer: check at once, the 60 s check is the backstop
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return NoWatch.Instance;   // boundary: no folder, no inotify slot: poll only
        }
    }

    private sealed class NoWatch : IDisposable
    {
        internal static readonly NoWatch Instance = new();

        public void Dispose()
        {
            // nothing to stop
        }
    }
}
