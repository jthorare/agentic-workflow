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
            var state = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = 1,
                ["finished"] = run.First().Finished,
                ["resolvedBy"] = run.First().ResolvedBy,
                ["candidates"] = run.Select(static c => new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["id"] = c.Candidate,
                    ["worktree"] = c.Worktree,
                    ["branch"] = c.Branch,
                    ["commit"] = c.Commit,
                    ["evaluated"] = c.Evaluated,
                    ["resolves"] = c.Resolves,
                }).ToList(),
            };
            var directory = Path.Combine(root, "owner-repo", run.Key);
            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "state.json"), JsonSerializer.Serialize(state));
        }

        return root;
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
