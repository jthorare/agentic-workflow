using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class DirectoryWorktreeFilesContractTests : WorktreeFilesContract, IDisposable
{
    private readonly List<string> _directories = [];

    private string? _environmentRoot;

    protected override IWorktreeFiles Create() => new DirectoryWorktreeFiles(name => string.Equals(name, "AW_WORKTREE_ROOT", StringComparison.Ordinal) ? _environmentRoot : null);

    protected override string ArrangeRoot(string? fileRoot, string? environmentRoot)
    {
        var repo = Directory.CreateTempSubdirectory("aw-meta-root-").FullName;
        _directories.Add(repo);
        _environmentRoot = environmentRoot;
        if (fileRoot is not null)
        {
            _ = Directory.CreateDirectory(Path.Combine(repo, ".agentic-workflow"));
            File.WriteAllText(Path.Combine(repo, ".agentic-workflow", "worktrees.json"), System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { ["root"] = fileRoot }));
        }

        return repo;
    }

    // Ideas' review of #2847: a malformed file reads as "not configured", so the caller falls back to the default root. Only the
    // file adapter reads a file, so these cases live here, not in the shared contract.
    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"root\": 5}")]
    [InlineData("{\"root\": \"\"}")]
    [InlineData("[\"/a\"]")]
    [InlineData("{}")]
    public async Task A_malformed_worktrees_file_reads_as_not_configured(string text)
    {
        var repo = ArrangeRoot(fileRoot: null, environmentRoot: null);
        _ = Directory.CreateDirectory(Path.Combine(repo, ".agentic-workflow"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".agentic-workflow", "worktrees.json"), text, TestContext.Current.CancellationToken);

        var result = await Create().ConfiguredRoot(repo, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess && result.Value is null, text);
    }

    protected override string Arrange(IReadOnlyDictionary<string, DateTimeOffset> entries)
    {
        var root = Directory.CreateTempSubdirectory("aw-meta-files-").FullName;
        _directories.Add(root);
        foreach (var path in entries.Keys.Where(k => k.Length > 0))
        {
            var full = Path.Combine(root, path.TrimEnd('/'));
            if (path.EndsWith('/'))
            {
                _ = Directory.CreateDirectory(full);
            }
            else
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, "x");
            }
        }

        // Stamp the deepest entries first: writing into a directory moves its own time.
        foreach (var (path, time) in entries.OrderByDescending(e => e.Key.Count(c => c == '/')).ThenByDescending(e => e.Key.Length))
        {
            var full = Path.Combine(root, path.TrimEnd('/'));
            if (Directory.Exists(full))
            {
                Directory.SetLastWriteTimeUtc(full, time.UtcDateTime);
            }
            else
            {
                File.SetLastWriteTimeUtc(full, time.UtcDateTime);
            }
        }

        return root;
    }

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
