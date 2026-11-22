namespace Aw.Sindri.Ui.Platform;

/// <summary>A launcher that opens a file in the configured editor (#3715) and hands everything else, and any file no editor took, to the launcher it wraps.</summary>
/// <param name="inner">The OS default launcher.</param>
/// <param name="environment">Reads the editor settings at each request.</param>
/// <param name="start">Starts an argv list with no shell; true when the process started.</param>
internal sealed class EditorLauncher(ILauncher inner, Func<EditorEnvironment> environment, Func<IReadOnlyList<string>, bool> start) : ILauncher
{
    public Task<bool> OpenUri(Uri uri, CancellationToken cancellationToken) => inner.OpenUri(uri, cancellationToken);

    public Task<bool> OpenFile(string path, CancellationToken cancellationToken)
    {
        _ = (environment, start, path, cancellationToken);   // red: the editor is not wired yet
        throw new NotSupportedException();
    }
}
