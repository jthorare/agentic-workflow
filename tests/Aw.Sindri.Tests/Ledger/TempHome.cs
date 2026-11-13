using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>
/// A throwaway Sindri home under the temp directory, deleted on dispose. A Temporal dev server still running on its
/// database is killed first: the server runs detached (setsid), so a test that failed, timed out or never stopped it
/// would leave it running after the run, on a database that no longer exists.
/// </summary>
public sealed class TempHome : IDisposable
{
    public TempHome()
    {
        _ = Directory.CreateDirectory(Path);
    }

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aw-sindri-" + System.IO.Path.GetRandomFileName());

    public void Dispose()
    {
        foreach (var server in new TemporalDevServer(ChildEnvironment.Start).Find(Path))
        {
            DevServerProcesses.Signal(ChildEnvironment.Start, server.ProcessId, "KILL");
        }

        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        finally
        {
            // A launch's session home is a sibling of the home (<home>-sessions/<launch>), so deleting the home leaves it.
            var sessions = SessionHomePaths.Sessions(Path);
            if (Directory.Exists(sessions))
            {
                try
                {
                    Directory.Delete(sessions, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
