using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

/// <summary>The state.json reader over a temp $AW_STATE_DIR/bugfix: the contract, plus the files it must skip.</summary>
[Lane(TestLane.Integration)]
public sealed class FileBugfixRecordsContractTests : BugfixRecordsContract, IDisposable
{
    private readonly List<string> _directories = [];

    protected override IBugfixRecords Create(params BugfixCandidate[] candidates) => new FileBugfixRecords(Write(candidates));

    // One state.json per run under <root>/owner-repo/<run>/, in bugfix-state's own shape.
    private string Write(BugfixCandidate[] candidates)
    {
        var root = Directory.CreateTempSubdirectory("aw-meta-bugfix-").FullName;
        _directories.Add(root);
        foreach (var run in candidates.GroupBy(static c => c.Run, StringComparer.Ordinal))
        {
            var directory = Path.Combine(root, "owner-repo", run.Key);
            _ = Directory.CreateDirectory(directory);
            var state = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = 1,
                ["finished"] = run.First().Finished,
                ["resolvedBy"] = run.First().ResolvedBy,
                ["ticket"] = run.First().Issue is { } issue
                    ? new Dictionary<string, string>(StringComparer.Ordinal) { ["source"] = "github", ["id"] = "#" + issue.ToString(CultureInfo.InvariantCulture) }
                    : new Dictionary<string, string>(StringComparer.Ordinal) { ["source"] = "text", ["id"] = "pasted" },
                ["baseline"] = run.FirstOrDefault(static c => c.Baseline) is { } red ? Baseline(directory, red) : null,
                ["candidates"] = run.Where(static c => !c.Baseline).Select(static c => new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["id"] = c.Candidate,
                    ["worktree"] = c.Worktree,
                    ["branch"] = c.Branch,
                    ["commit"] = c.Commit,
                    ["evaluated"] = c.Evaluated,
                    ["resolves"] = c.Resolves,
                }).ToList(),
            };
            File.WriteAllText(Path.Combine(directory, "state.json"), JsonSerializer.Serialize(state));
        }

        return root;
    }

    // bugfix-state's baseline: the reproduce run's evidence file (its checkout is the red tree) and the commit.
    private static Dictionary<string, string> Baseline(string directory, BugfixCandidate red)
    {
        var evidence = Path.Combine(directory, "runs", "test-1.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);
        File.WriteAllText(evidence, JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { ["kind"] = "test", ["outcome"] = "failed", ["commit"] = red.Commit, ["checkout"] = red.Worktree }));
        return new Dictionary<string, string>(StringComparer.Ordinal) { ["evidence"] = evidence, ["commit"] = red.Commit };
    }

    [Fact]
    public async Task A_state_file_that_is_not_json_is_skipped_and_the_others_still_read()
    {
        var root = Write([new BugfixCandidate("gh-9", "c1", "/wt/gh-9-a1", "b", "s", Evaluated: true, Resolves: false, Finished: true, ResolvedBy: "c2")]);
        var broken = Path.Combine(root, "owner-repo", "gh-10");
        _ = Directory.CreateDirectory(broken);
        await File.WriteAllTextAsync(Path.Combine(broken, "state.json"), "{ not json", TestContext.Current.CancellationToken);

        var result = await new FileBugfixRecords(root).Naming("/wt/gh-9-a1", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Value);
    }

    [Fact]
    public async Task A_state_file_rewritten_between_calls_is_read_again()
    {
        var root = Write([new BugfixCandidate("gh-9", "c1", "/wt/gh-9-a1", "b", "s", Evaluated: true, Resolves: false, Finished: false, ResolvedBy: null)]);
        var records = new FileBugfixRecords(root);
        _ = await records.Naming("/wt/gh-9-a1", TestContext.Current.CancellationToken);
        var state = Path.Combine(root, "owner-repo", "gh-9", "state.json");
        await File.WriteAllTextAsync(state, (await File.ReadAllTextAsync(state, TestContext.Current.CancellationToken)).Replace("\"finished\":false", "\"finished\": true", StringComparison.Ordinal), TestContext.Current.CancellationToken);

        var result = await records.Naming("/wt/gh-9-a1", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(Assert.Single(result.Value).Finished);
    }

    [Fact]
    public async Task A_broken_baseline_evidence_file_names_no_red_tree_and_the_candidates_still_read()
    {
        var root = Write([new BugfixCandidate("gh-9", "c1", "/wt/gh-9-a1", "b", "s", Evaluated: true, Resolves: false, Finished: true, ResolvedBy: "c2") { Issue = 9 },
            new BugfixCandidate("gh-9", "red", "/wt/gh-9", string.Empty, "r", Evaluated: true, Resolves: false, Finished: true, ResolvedBy: "c2") { Issue = 9, Baseline = true }]);
        await File.WriteAllTextAsync(Path.Combine(root, "owner-repo", "gh-9", "runs", "test-1.json"), "{ not json", TestContext.Current.CancellationToken);
        var records = new FileBugfixRecords(root);

        var red = await records.Naming("/wt/gh-9", TestContext.Current.CancellationToken);
        var candidate = await records.Naming("/wt/gh-9-a1", TestContext.Current.CancellationToken);

        Assert.True(red.IsSuccess && candidate.IsSuccess);
        Assert.Empty(red.Value);
        _ = Assert.Single(candidate.Value);
    }

    [Fact]
    public async Task A_missing_bugfix_directory_gives_an_empty_list()
    {
        var result = await new FileBugfixRecords(Path.Combine(Path.GetTempPath(), "aw-meta-no-such-" + RealTime.NewGuid().ToString("N"))).Naming("/wt/x", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
