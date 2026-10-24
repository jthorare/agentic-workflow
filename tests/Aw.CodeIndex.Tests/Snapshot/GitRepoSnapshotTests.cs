using System.Globalization;
using System.Text;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Infrastructure.Snapshot;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Snapshot;

public sealed class GitRepoSnapshotTests
{
    private static readonly SnapshotLimits _limits = new([], MaxFileBytes: 1_000_000, MaxTotalBytes: 10_000_000);
    private readonly GitRepoSnapshot _snapshot = new();

    [Fact, Trait("Lane", "unit")]
    public void The_git_environment_is_scrubbed_and_the_hardening_variables_are_set()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["GIT_DIR"] = "/elsewhere",
            ["GIT_WORK_TREE"] = "/elsewhere",
            ["GIT_INDEX_FILE"] = "/elsewhere/index",
            ["GIT_OBJECT_DIRECTORY"] = "/elsewhere/objects",
            ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = "/elsewhere/alt",
            ["GIT_NAMESPACE"] = "ns",
            ["GIT_CEILING_DIRECTORIES"] = "/",
            ["GIT_COMMON_DIR"] = "/elsewhere",
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "core.hooksPath",
            ["GIT_CONFIG_VALUE_0"] = "/evil",
            ["GIT_CONFIG_PARAMETERS"] = "'core.pager=evil'",
            ["GIT_CONFIG_GLOBAL"] = "/home/x/.gitconfig",
            ["GIT_TRACE"] = "1",
            ["PATH"] = "/usr/bin",
        };

        CleanGit.ScrubEnvironment(environment);

        Assert.Equal("/usr/bin", environment["PATH"]);
        Assert.Equal(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["GIT_OPTIONAL_LOCKS"] = "0",
                ["GIT_NO_LAZY_FETCH"] = "1",
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["GIT_CONFIG_GLOBAL"] = "/dev/null",
            },
            environment.Where(e => e.Key.StartsWith("GIT_", StringComparison.Ordinal)).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_git_call_carries_the_hardening_arguments_and_never_trusts_a_directory()
    {
        var arguments = string.Join(' ', CleanGit.GlobalArguments("/the/repo"));

        Assert.StartsWith("-C /the/repo ", arguments, StringComparison.Ordinal);
        foreach (var required in new[] { "--no-replace-objects", "protocol.allow=never", "core.fsmonitor=false", "core.hooksPath=/dev/null", "core.quotepath=off" })
        {
            Assert.Contains(required, arguments, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("safe.directory", arguments, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_path_that_is_not_a_git_repo_is_SND_CODEIDX_002()
    {
        using var directory = new TempDirectory();
        var repo = new CodeIndexRepo(RepoName.From("none"), directory.Path, Core.CodeIndexSettings.Default);
        var commit = CommitSha.From("0123456789abcdef0123456789abcdef01234567");

        var inventory = await _snapshot.Inventory(repo, commit, _limits, TestContext.Current.CancellationToken);
        var blobs = await _snapshot.OpenBlobs(repo, TestContext.Current.CancellationToken);

        Assert.Equal("SND-CODEIDX-002", inventory.Error.Code);
        Assert.Equal("SND-CODEIDX-002", blobs.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_path_swapped_for_a_symlink_after_the_commit_still_reads_the_committed_content()
    {
        using var git = A.GitRepo().WithFile("src/A.cs", "committed").Build();
        git.SwapWithSymlink("src/A.cs", "/etc/hostname");

        var inventory = (await _snapshot.Inventory(git.AsRepo(), git.Head, _limits, TestContext.Current.CancellationToken)).Value;
        await using var reader = (await _snapshot.OpenBlobs(git.AsRepo(), TestContext.Current.CancellationToken)).Value;
        var bytes = (await reader.Read(inventory.Files.Single().Blob, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("committed", Encoding.UTF8.GetString(bytes!));
        Assert.Empty(inventory.Skipped);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_replace_ref_does_not_change_what_a_blob_id_means()
    {
        using var git = A.GitRepo().WithFile("a.txt", "the real content").Build();
        var real = git.Git("rev-parse", "HEAD:a.txt");
        git.WriteFile("decoy.txt", "the decoy content");
        var decoy = git.Git("hash-object", "-w", "decoy.txt");
        _ = git.Git("replace", real, decoy);
        await using var reader = (await _snapshot.OpenBlobs(git.AsRepo(), TestContext.Current.CancellationToken)).Value;

        var bytes = (await reader.Read(real, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("the real content", Encoding.UTF8.GetString(bytes!));
        Assert.Equal("the decoy content", git.Git("cat-file", "blob", real));   // a plain git would have followed the replace ref
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_promisor_clone_is_never_asked_for_its_missing_blobs()
    {
        using var clone = A.GitRepo().WithFile("src/A.cs", "class A {}").WithFile("README.md", "docs").BuildPromisorClone();
        var before = Objects(clone);

        var inventory = (await _snapshot.Inventory(clone.AsRepo(), clone.Head, _limits, TestContext.Current.CancellationToken)).Value;
        await using var reader = (await _snapshot.OpenBlobs(clone.AsRepo(), TestContext.Current.CancellationToken)).Value;
        var read = await reader.Read("0123456789012345678901234567890123456789", TestContext.Current.CancellationToken);

        Assert.Empty(inventory.Files);
        Assert.Equal(2, inventory.Skipped[SkipReason.Missing]);
        Assert.Null(read.Value);
        Assert.Equal(before, Objects(clone));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_large_blob_and_many_reads_through_one_reader_stay_in_step()
    {
        var big = string.Concat(Enumerable.Range(0, 20_000).Select(i => string.Create(CultureInfo.InvariantCulture, $"line {i}\n")));
        using var git = A.GitRepo().WithFile("big.txt", big).WithFile("small.txt", "small").Build();
        var inventory = (await _snapshot.Inventory(git.AsRepo(), git.Head, _limits, TestContext.Current.CancellationToken)).Value;
        await using var reader = (await _snapshot.OpenBlobs(git.AsRepo(), TestContext.Current.CancellationToken)).Value;
        var blobs = inventory.Files.ToDictionary(f => f.Path, f => f.Blob, StringComparer.Ordinal);

        for (var round = 0; round < 3; round++)
        {
            var bigBytes = (await reader.Read(blobs["big.txt"], TestContext.Current.CancellationToken)).Value;
            var smallBytes = (await reader.Read(blobs["small.txt"], TestContext.Current.CancellationToken)).Value;

            Assert.Equal(big, Encoding.UTF8.GetString(bigBytes!));
            Assert.Equal("small", Encoding.UTF8.GetString(smallBytes!));
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_cancelled_inventory_throws()
    {
        using var git = A.GitRepo().WithFile("a.txt", "x").Build();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await _snapshot.Inventory(git.AsRepo(), git.Head, _limits, cancelled.Token));
    }

    private static string[] Objects(TempGitRepo repo)
        => [.. Directory.EnumerateFiles(Path.Combine(repo.Path, ".git", "objects"), "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(repo.Path, f)).Order(StringComparer.Ordinal)];

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("aw-codeindex-plain-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
