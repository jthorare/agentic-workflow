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

    /// <summary>The file: URI of a path, each segment escaped, so a '#' or '?' in a file name is part of the path (#3715).</summary>
    /// <param name="path">A path.</param>
    /// <returns>The URI.</returns>
    internal static Uri FileUri(string path)
    {
        // 'new Uri(path)' leaves '#' and '?' to the URI grammar; escaping each segment makes them part of the file name. A drive letter ("C:") stays as is.
        var segments = Path.GetFullPath(path).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]).Select(s => s.Length == 2 && s[1] == ':' ? s : Uri.EscapeDataString(s));
        var escaped = string.Join('/', segments);
        return new Uri("file://" + (escaped.StartsWith('/') ? escaped : "/" + escaped), UriKind.Absolute);
    }

    public async Task<bool> OpenFile(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (window() is not { } top || !Path.IsPathRooted(path))
            {
                return false;
            }

            var file = await top.StorageProvider.TryGetFileFromPathAsync(FileUri(path)).ConfigureAwait(true);
            return file is not null && await top.Launcher.LaunchFileAsync(file).ConfigureAwait(true);
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or UriFormatException or IOException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public bool FileExists(string path) => Path.IsPathRooted(path) && File.Exists(path);
}
