namespace Aw.Sindri.Ui.Platform;

/// <summary>Port: hands a link or a file to the system to open (UI spec §7: opening apps). The app supplies one adapter (the Avalonia one, which every head shares); the view models guard what they pass. An adapter answers false for a refusal and never throws.</summary>
public interface ILauncher
{
    /// <summary>Opens a link in the system's browser or app.</summary>
    /// <param name="uri">The link; the caller has already checked its scheme.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the system took it; false when it refused or none is available.</returns>
    public Task<bool> OpenUri(Uri uri, CancellationToken cancellationToken);

    /// <summary>Opens a file in the system's app for it.</summary>
    /// <param name="path">An absolute path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the system opened it; false when the file is not there, or the system refused.</returns>
    public Task<bool> OpenFile(string path, CancellationToken cancellationToken);

    /// <summary>Tells whether a file is on this machine, so a refused <see cref="OpenFile"/> can say why (#3714). Desktop only today: a head that reaches files through a sandbox must answer from its own storage.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>True when the path is absolute and a file is there; false for a relative path or no file.</returns>
    public bool FileExists(string path);
}
