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
        try
        {
            foreach (var server in new TemporalDevServer(ChildEnvironment.Start).Find(Path))
            {
                DevServerProcesses.Signal(ChildEnvironment.Start, server.ProcessId, "KILL");
            }
        }
        finally
        {
            // why: a failed process scan must not leave the home on disk (#3262)
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
