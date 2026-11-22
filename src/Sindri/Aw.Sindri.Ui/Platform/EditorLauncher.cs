using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Ui.Platform;

/// <summary>A launcher that opens a file in the configured editor (#3715) and hands everything else, and any file no editor took, to the launcher it wraps.</summary>
/// <param name="inner">The OS default launcher.</param>
/// <param name="environment">Reads the editor settings at each request.</param>
/// <param name="start">Starts an argv list with no shell; true when the process started.</param>
internal sealed class EditorLauncher(ILauncher inner, Func<EditorEnvironment> environment, Func<IReadOnlyList<string>, bool> start) : ILauncher
{
    /// <summary>The launcher of the running app: the editor from this process's environment, started directly.</summary>
    /// <param name="inner">The OS default launcher.</param>
    /// <returns>The launcher.</returns>
    internal static EditorLauncher OfProcess(ILauncher inner) => new(inner, EditorEnvironment.Read, StartDetached);

    public Task<bool> OpenUri(Uri uri, CancellationToken cancellationToken) => inner.OpenUri(uri, cancellationToken);

    public Task<bool> OpenFile(string path, CancellationToken cancellationToken)
    {
        if (Path.IsPathRooted(path) && File.Exists(path) && EditorCommand.Resolve(environment()) is { } argv && start([.. argv, path]))
        {
            return Task.FromResult(true);
        }

        return inner.OpenFile(path, cancellationToken);   // no editor set, or it would not start: the system's app for the file
    }

    // The argv list goes to the OS as separate arguments (no shell reads it); the editor runs on its own and is not waited for.
    private static bool StartDetached(IReadOnlyList<string> argv)
    {
        var info = new ProcessStartInfo(argv[0]) { UseShellExecute = false };
        foreach (var argument in argv.Skip(1))
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(info);
            return process is not null;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
