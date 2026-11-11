using System.Text.RegularExpressions;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The only way a test in this folder cancels a graph run is <see cref="GraphRig.Cancel"/>, which waits on the workflow's own result (issue 2678).
/// A direct cancel on a workflow handle is followed by a polling wait under a fixed wall-clock deadline, which fails on a loaded box.
/// </summary>
[Lane(TestLane.Unit)]
public sealed partial class GraphRigCancelGuardTests
{
    // why: only workflow handles; a CancellationTokenSource's CancelAsync (a '_stop' or 'cts' receiver) is not a run cancel
    [GeneratedRegex(@"GetWorkflowHandle\([^;]*\)\s*\.CancelAsync\(|\bhandle\s*\.CancelAsync\(", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DirectCancel { get; }

    [Fact]
    public void No_test_cancels_a_run_without_the_rig()
    {
        var folder = Path.Combine(RepoRoot(), "tests", "Aw.Sindri.Engine.Tests", "Workflows");
        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, "GraphRig.cs", StringComparison.Ordinal) || string.Equals(name, "GraphRigCancelGuardTests.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (DirectCancel.IsMatch(lines[i]))
                {
                    hits.Add($"{Path.GetRelativePath(folder, file)}:{(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }
            }
        }

        Assert.True(hits.Count == 0, "These lines cancel a run directly; use GraphRig.Cancel:\n" + string.Join('\n', hits));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test binaries");
    }
}
