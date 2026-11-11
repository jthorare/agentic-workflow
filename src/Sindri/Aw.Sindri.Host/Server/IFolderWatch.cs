namespace Aw.Sindri.Host.Server;

/// <summary>Watches one file's folder for changes (todo spec §4.5 "Reading"): a trigger only, because file-system watchers miss events on some disks.</summary>
internal interface IFolderWatch
{
    /// <summary>Starts a non-recursive watch on <paramref name="folder"/>, filtered to <paramref name="fileName"/>.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="fileName">The one file name that counts.</param>
    /// <param name="changed">Called, on any thread, when that file may have changed.</param>
    /// <returns>Stops the watch when disposed. A folder that can't be watched gives a handle that does nothing (the 60 s check still runs).</returns>
    public IDisposable Watch(string folder, string fileName, Action changed);
}
