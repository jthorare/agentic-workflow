using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Infrastructure.Line;

/// <summary>Reads a checkout's files and the environment for 'line doctor': a bounded walk for a test file, the evidence targets file, the credential's variable.</summary>
/// <param name="environment">Reads an environment variable.</param>
internal sealed class FileLineSetupProbe(Func<string, string?> environment) : ILineSetupProbe
{
    private const int MaxFiles = 50_000;
    private static readonly string[] _skipped = [".git", "node_modules", "bin", "obj", ".venv", "dist"];

    /// <inheritdoc/>
    public ValueTask<LineSetupFacts> Read(string checkout, string? credentialVariable, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        cancellationToken.ThrowIfCancellationRequested();
        var credential = credentialVariable is null ? (bool?)null : !string.IsNullOrEmpty(environment(credentialVariable));
        return ValueTask.FromResult(new LineSetupFacts(HasTestFile(checkout, cancellationToken), File.Exists(Path.Combine(checkout, ".agentic-workflow", "evidence.json")), credential));
    }

    private static bool HasTestFile(string checkout, CancellationToken cancellationToken)
    {
        var seen = 0;
        var pending = new Stack<string>([checkout]);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(entry);
                    if (Directory.Exists(entry))
                    {
                        if (Array.IndexOf(_skipped, name) < 0)
                        {
                            pending.Push(entry);
                        }
                    }
                    else if (TestPaths.IsTest(Path.GetRelativePath(checkout, entry).Replace('\\', '/')))
                    {
                        return true;
                    }

                    if (++seen > MaxFiles)
                    {
                        return false;
                    }
                }
            }
            catch (IOException)
            {
                // a directory that cannot be listed holds no test file we can name
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return false;
    }
}
