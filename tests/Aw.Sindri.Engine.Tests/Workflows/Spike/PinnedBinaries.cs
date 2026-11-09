namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// The executables binaries.lock pins, where 'scripts/binaries-lock.sh fetch &lt;name&gt;' installs them. A missing one fails
/// the test with the fetch command, so the SDK never downloads a server and a missing binary never reads as a pass.
/// </summary>
internal static class PinnedBinaries
{
    /// <summary>The installed path of <paramref name="name"/>'s pinned version (binaries.lock: name, version, ..., path).</summary>
    /// <param name="name">'temporal' or 'temporal-test-server'.</param>
    /// <returns>The executable, wrapped so the server dies with this test host (<see cref="ServerGuard"/>).</returns>
    public static string Path(string name)
    {
        var row = File.ReadLines(System.IO.Path.Combine(RepoRoot(), "binaries.lock"))
            .Select(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .First(f => f.Length == 6 && f[0] == name);
        var stateDir = Environment.GetEnvironmentVariable("AW_STATE_DIR") is { Length: > 0 } dir
            ? dir
            : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var path = System.IO.Path.Combine(stateDir, "cache", "bin", $"{name}-{row[1]}", System.IO.Path.GetFileName(row[5]));
        if (!File.Exists(path))
        {
            Assert.Fail($"the pinned {name} {row[1]} is not installed at {path}: run scripts/binaries-lock.sh fetch {name}");
        }

        return ServerGuard.Wrap(path);
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No AgenticWorkflow.slnx above the test's base directory.");
    }
}
