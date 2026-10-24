using Avalonia.Controls;

namespace Aw.UiDriver.Avalonia;

/// <summary>The running driver of this process: the files and the server, started once and stopped when the process ends.</summary>
internal static class DriverRuntime
{
    private static readonly Lock _gate = new();
    private static bool _started;
    private static DriverServer? _server;   // alive for the process: ProcessExit removes the files and the listener dies with it

    /// <summary>Starts the server and publishes the driver directory; a second call does nothing.</summary>
    /// <param name="directory">AW_UI_DRIVER_DIR, or null for a new folder in the temp directory.</param>
    /// <param name="window">The window to drive, asked for at each call (the app's window may open after this runs).</param>
    internal static void Start(string? directory, Func<Window?> window)
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
        }

        var folder = string.IsNullOrWhiteSpace(directory) ? Path.Combine(Path.GetTempPath(), "aw-ui-driver-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)) : directory;
        var files = DriverFiles.Create(folder);
        _server = DriverServer.Start(new DriverHost(window, TimeProvider.System), files.Secret);
        if (_server is null)
        {
            Console.Error.WriteLine("aw-ui-driver: no free loopback port, the driver is off");
            files.Remove();
            return;
        }

        files.PublishEndpoint(_server.Origin);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => files.Remove();
        Console.Error.WriteLine("aw-ui-driver: listening, dir " + folder);
    }
}
