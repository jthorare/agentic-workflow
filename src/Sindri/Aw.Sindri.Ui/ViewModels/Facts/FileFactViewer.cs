using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>A file fact: its name, size and hash as served, and Open, which the platform answers (a path the app can't resolve says so).</summary>
internal sealed class FileFactViewer : ObservableObject, IFactViewer
{
    private const int HashChars = 12;

    private readonly LinkLaunch _launch;

    internal FileFactViewer(FactRow row, ILauncher launcher)
    {
        Row = row;
        _launch = new LinkLaunch(launcher);
        var file = row.Ref;
        Name = FactText.Scrub(file?.Name ?? row.Summary);
        SizeText = file?.Size is { } size ? Labels.FileSize(size) : string.Empty;
        HashText = file?.Hash is { } hash ? Short(hash) : string.Empty;
        Path = FactText.Scrub(file?.Path ?? string.Empty);
        OpenName = Labels.OpenFileName(Path.Length > 0 ? Path : Name);
    }

    /// <summary>The fact row the viewer shows.</summary>
    public FactRow Row { get; }

    /// <summary>The file's name as served.</summary>
    public string Name { get; }

    /// <summary>The size in words ("18 KB").</summary>
    public string SizeText { get; }

    /// <summary>The hash: its algorithm and the first twelve characters.</summary>
    public string HashText { get; }

    /// <summary>The path as served.</summary>
    public string Path { get; }

    /// <summary>True when a path is served, so there is something to open.</summary>
    public bool CanOpen => Path.Length > 0;

    /// <summary>Open's accessible name.</summary>
    public string OpenName { get; }

    /// <summary>What Open found, in words; empty before it was pressed.</summary>
    public string OpenNote { get; private set { Set(ref field, value); OnChanged(nameof(HasOpenNote)); } } = string.Empty;

    /// <summary>True when Open has something to say.</summary>
    public bool HasOpenNote => OpenNote.Length > 0;

    /// <summary>A file viewer needs nothing beyond the row.</summary>
    /// <param name="cancellationToken">Cancels nothing.</param>
    /// <returns>A completed task.</returns>
    public Task Load(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Asks the system to open the file the fact names, and says so when it could not (a path that is not absolute is never opened).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes once the system answered.</returns>
    public async Task Open(CancellationToken cancellationToken) =>
        ReportOpened(System.IO.Path.IsPathRooted(Path) && await _launch.OpenPath(Path, cancellationToken).ConfigureAwait(true));

    /// <summary>Records what Open found.</summary>
    /// <param name="opened">True when the platform opened the file.</param>
    public void ReportOpened(bool opened) => OpenNote = opened ? string.Empty : Labels.FileNotOpened(Path);

    private static string Short(string hash)
    {
        var colon = hash.IndexOf(':', StringComparison.Ordinal);
        return FactText.Scrub(hash.Length > colon + 1 + HashChars ? hash[..(colon + 1 + HashChars)] : hash);
    }
}
