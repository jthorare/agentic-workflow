using System.Text.RegularExpressions;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The only way a test in this folder cancels a graph run is <see cref="GraphRig.Cancel"/>, which polls the run row under the
/// 5 min hang limit instead of the 30 s check patience (issue 2678). A direct cancel on a workflow handle is followed by a
/// polling wait under that fixed wall-clock deadline, which fails on a loaded box.
/// </summary>
[Lane(TestLane.Unit)]
public sealed partial class GraphRigCancelGuardTests
{
    // why: any receiver counts (issue 2843: 'var h = ...GetWorkflowHandle(id); h.CancelAsync()' slipped past a name-based match); ')' is a call result
    [GeneratedRegex(@"(?<receiver>\w+|\))\s*\.CancelAsync\(", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AnyCancel { get; }

    // why: a token source's CancelAsync is not a run cancel; the file must declare the name as one ('CancellationTokenSource x', 'x = new CancellationTokenSource', 'x = CancellationTokenSource.Create...')
    [GeneratedRegex(@"CancellationTokenSource\s+(?<name>\w+)|(?<name>\w+)\s*=\s*(?:new\s+CancellationTokenSource\b|CancellationTokenSource\.Create)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenSource { get; }

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

            foreach (var line in DirectCancels(File.ReadAllText(file)))
            {
                hits.Add($"{Path.GetRelativePath(folder, file)}:{line.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }
        }

        Assert.True(hits.Count == 0, "These lines cancel a run directly; use GraphRig.Cancel:\n" + string.Join('\n', hits));
    }

    [Fact]
    public void A_cancel_through_a_handle_in_any_variable_is_flagged()
    {
        var source = string.Join('\n',
            "var h = client.GetWorkflowHandle(id);",
            "await h.CancelAsync();",
            "_ = await rig.Final(id);");

        Assert.Equal([2], DirectCancels(source));
    }

    [Fact]
    public void A_cancel_on_a_field_or_a_call_result_is_flagged()
    {
        var source = string.Join('\n',
            "await _run.CancelAsync();",
            "await Handle().CancelAsync();");

        Assert.Equal([1, 2], DirectCancels(source));
    }

    [Fact]
    public void A_cancel_of_a_token_source_is_not_flagged()
    {
        var source = string.Join('\n',
            "private readonly CancellationTokenSource _stop = new();",
            "using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);",
            "var cts = new CancellationTokenSource();",
            "await _stop.CancelAsync();",
            "await limit.CancelAsync();",
            "await cts.CancelAsync();");

        Assert.Empty(DirectCancels(source));
    }

    /// <summary>The 1-based lines of <paramref name="source"/> that cancel something other than a token source it declares.</summary>
    internal static IReadOnlyList<int> DirectCancels(string source)
    {
        var sources = TokenSource.Matches(source).Select(static m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);
        var lines = source.Split('\n');
        var hits = new List<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (AnyCancel.Matches(lines[i]).Any(m => !sources.Contains(m.Groups["receiver"].Value)))
            {
                hits.Add(i + 1);
            }
        }

        return hits;
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
