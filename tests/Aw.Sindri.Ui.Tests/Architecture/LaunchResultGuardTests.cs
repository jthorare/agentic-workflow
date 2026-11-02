using System.Globalization;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Architecture;

/// <summary>Every link or file launch in the app goes through LinkLaunch, which keeps the system's answer; a call that drops it again fails here (#1712, #1722).</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchResultGuardTests
{
    // The helper, the ILauncher contract, and the adapters that implement it.
    private static readonly string[] _allowed = ["ViewModels/LinkLaunch.cs", "Platform/ILauncher.cs", "Platform/NullLauncher.cs", "Views/AvaloniaLauncher.cs"];

    [Fact]
    public void No_code_calls_the_launcher_outside_the_helper()
    {
        var root = FindUiSource();
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal) || _allowed.Contains(relative))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if ((lines[i].Contains(".OpenUri(", StringComparison.Ordinal) || lines[i].Contains(".OpenFile(", StringComparison.Ordinal)) && !lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    offenders.Add($"src/Sindri/Aw.Sindri.Ui/{relative}:{(i + 1).ToString(CultureInfo.InvariantCulture)}: {lines[i].Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Launch through LinkLaunch so the answer is kept and a refusal is shown:\n" + string.Join('\n', offenders));
    }

    private static string FindUiSource()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Sindri", "Aw.Sindri.Ui");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("src/Sindri/Aw.Sindri.Ui not found above " + AppContext.BaseDirectory);
    }
}
