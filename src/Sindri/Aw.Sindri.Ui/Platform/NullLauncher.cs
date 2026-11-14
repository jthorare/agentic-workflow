namespace Aw.Sindri.Ui.Platform;

/// <summary>The launcher of a head that has none: nothing opens.</summary>
internal sealed class NullLauncher : ILauncher
{
    internal static NullLauncher Instance { get; } = new();

    public Task<bool> OpenUri(Uri uri, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> OpenFile(string path, CancellationToken cancellationToken) => Task.FromResult(false);

    public bool FileExists(string path) => false;
}
