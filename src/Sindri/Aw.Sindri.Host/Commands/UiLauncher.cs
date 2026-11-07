using System.Diagnostics;
using System.Globalization;
using Aw.Kernel.Data;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Bare 'sindri' opens the Sindri app (docs/csharp/sindri-ui.md): the 'sindri-ui' binary next to this one, or the one
/// AW_SINDRI_UI names. It starts through /bin/sh with exec and nohup, so it outlives this command and its terminal and
/// keeps none of its pipes. What happened is the summary's ui line; it never changes the exit code. 'sindri --headless'
/// doesn't call it.
/// </summary>
internal static class UiLauncher
{
    internal const string Variable = "AW_SINDRI_UI";

    internal static string Locate(Func<string, string?> environment, string baseDirectory) =>
        environment(Variable) is { Length: > 0 } path ? path : Path.Combine(baseDirectory, "sindri-ui");

    internal static string Open(string location)
    {
        // A relative path is resolved here, where it is checked: exec would otherwise search PATH for it.
        var path = Path.GetFullPath(location);
        if (!File.Exists(path))
        {
            return $"not found at {path} (fix: publish Aw.Sindri.Ui.Desktop next to sindri, or set {Variable}); use sindri status";
        }

        if (!ExecutableFiles.IsExecutable(path))
        {
            return $"not executable at {path} (fix: chmod +x it, or publish Aw.Sindri.Ui.Desktop next to sindri); use sindri status";
        }

        try
        {
            using var process = Process.Start(Composition.StartChild("/bin/sh", ["-c", "exec nohup \"$0\" </dev/null >/dev/null 2>&1", path]));
            return process is null
                ? "couldn't start /bin/sh; use sindri status"
                : string.Create(CultureInfo.InvariantCulture, $"opened ({Path.GetFileName(path)}, pid {process.Id})");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            return $"couldn't start {path}: {e.Message}; use sindri status";
        }
    }
}
