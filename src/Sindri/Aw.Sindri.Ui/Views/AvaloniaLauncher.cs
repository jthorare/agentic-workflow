using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// <see cref="Platform.ILauncher"/> over Avalonia's launcher, which every head shares (it belongs to the window, so the app holds it, as it does the
/// clipboard: a head holds no Avalonia type, ARCH-UI-001). The window is looked up at each request: it is created after the view models. A file is
/// resolved through the storage provider first, so a path that is not there is answered false, not thrown.
/// </summary>
/// <param name="window">The top-level the request goes through, or null before it shows.</param>
internal sealed class AvaloniaLauncher(Func<TopLevel?> window) : Platform.ILauncher
{
    public async Task<bool> OpenUri(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            return window() is { } top && await top.Launcher.LaunchUriAsync(uri).ConfigureAwait(true);
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or UriFormatException or IOException or System.ComponentModel.Win32Exception)
        {
            return false;   // the platform's refusal is an answer, not a fault the caller's discarded task would swallow
        }
    }

    public async Task<bool> OpenFile(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (window() is not { } top || !Path.IsPathRooted(path))
            {
                return false;
            }

            var file = await top.StorageProvider.TryGetFileFromPathAsync(new Uri(path)).ConfigureAwait(true);
            return file is not null && await top.Launcher.LaunchFileAsync(file).ConfigureAwait(true);
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or UriFormatException or IOException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public bool FileExists(string path) => Path.IsPathRooted(path) && File.Exists(path);
}
