using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory launcher: what was opened, or a refusal when the test says so.</summary>
internal sealed class FakeLauncher : ILauncher
{
    private readonly List<string> _opened = [];
    private readonly HashSet<string> _files = new(StringComparer.Ordinal);

    /// <summary>Puts a file on the fake's disk: only these open (a relative path or a missing file is refused, as the adapter does).</summary>
    /// <param name="path">The absolute path.</param>
    public void AddFile(string path) => _ = _files.Add(path);

    /// <summary>Everything opened, in order: the link's text or the file's path.</summary>
    public IReadOnlyList<string> Opened => _opened;

    /// <summary>When true, every request is refused.</summary>
    public bool Refuse { get; set; }

    public Task<bool> OpenUri(Uri uri, CancellationToken cancellationToken) => Record(uri.AbsoluteUri);

    public Task<bool> OpenFile(string path, CancellationToken cancellationToken) => Path.IsPathRooted(path) && _files.Contains(path) ? Record(path) : Task.FromResult(false);

    public bool FileExists(string path) => Path.IsPathRooted(path) && _files.Contains(path);

    private Task<bool> Record(string what)
    {
        if (Refuse)
        {
            return Task.FromResult(false);
        }

        _opened.Add(what);
        return Task.FromResult(true);
    }
}
