using System.Text;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// The <see cref="Core.Ports.IRepoSnapshot"/> contract (spec §5.1, §11), run against the in-memory fake and
/// against the git adapter on a temp repo.
/// </summary>
public abstract class RepoSnapshotContract
{
    private static readonly CommitSha _unknownCommit = CommitSha.From("ffffffffffffffffffffffffffffffffffffffff");
    private static readonly SnapshotLimits _limits = new([], MaxFileBytes: 100, MaxTotalBytes: 10_000);

    /// <summary>Makes the implementation hold <paramref name="layout"/> in one commit.</summary>
    /// <param name="layout">What the commit's tree holds.</param>
    /// <returns>The implementation, its repo and the commit.</returns>
    protected abstract ArrangedSnapshot Arrange(RepoLayout layout);

    /// <summary>Regular files are kept with their blob and size in path order, and symlinks and submodules are counted, not kept.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_inventory_keeps_regular_files_in_path_order_and_counts_symlinks_and_submodules()
    {
        using var repo = Arrange(new RepoLayout().File("src/B.cs", "class B {}").File("a.txt", "hello").Symlink("link.cs").Submodule("vendor/lib"));

        var inventory = Ok(await repo.Snapshot.Inventory(repo.Repo, repo.Commit, _limits, TestContext.Current.CancellationToken));

        Check(inventory.Commit == repo.Commit, "the inventory names the commit it describes");
        Check(inventory.Files.Select(f => f.Path).SequenceEqual(["a.txt", "src/B.cs"], StringComparer.Ordinal), "kept files are listed in ordinal path order");
        Check(inventory.Files.All(f => f.Blob.Length >= 40), "every kept file carries its git object id");
        Check(inventory.Files.Select(f => f.Size).SequenceEqual([5L, 10L]), "a size is the content's byte length");
        Check(inventory.Skipped.OrderBy(s => s.Key).Select(s => (s.Key, s.Value)).SequenceEqual([(SkipReason.Symlink, 1), (SkipReason.Submodule, 1)]), "symlinks and submodules are counted and nothing else is");
    }

    /// <summary>The built-in secret globs and the added ones deny; denied files are counted and not kept.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Secret_globs_and_added_globs_deny_without_regard_to_case()
    {
        using var repo = Arrange(new RepoLayout().File("src/a.cs", "a").File(".env", "K=1").File("keys/Server.PEM", "k").File("gen/x.cs", "x"));

        var inventory = Ok(await repo.Snapshot.Inventory(repo.Repo, repo.Commit, _limits with { DenyPaths = ["gen/**"] }, TestContext.Current.CancellationToken));

        Check(inventory.Files.Select(f => f.Path).SequenceEqual(["src/a.cs"], StringComparer.Ordinal), "only the file outside every glob is kept");
        Check(inventory.Skipped.GetValueOrDefault(SkipReason.Denied) == 3, "three denied files are counted");
    }

    /// <summary>A file over the per-file cap is skipped as too-large, at the cap it is kept.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_file_over_the_per_file_cap_is_skipped_as_too_large()
    {
        using var repo = Arrange(new RepoLayout().File("at-cap.txt", new string('a', 100)).File("over-cap.txt", new string('b', 101)));

        var inventory = Ok(await repo.Snapshot.Inventory(repo.Repo, repo.Commit, _limits, TestContext.Current.CancellationToken));

        Check(inventory.Files.Select(f => f.Path).SequenceEqual(["at-cap.txt"], StringComparer.Ordinal), "a file exactly at the cap is kept");
        Check(inventory.Skipped.GetValueOrDefault(SkipReason.TooLarge) == 1, "the larger file is counted as too-large");
    }

    /// <summary>Kept sizes over the total cap stop with SND-CODEIDX-003; one byte under passes.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Kept_sizes_over_the_total_cap_stop_with_SND_CODEIDX_003()
    {
        using var repo = Arrange(new RepoLayout().File("a.cs", new string('a', 60)).File("b.ts", new string('b', 40)));

        var atCap = await repo.Snapshot.Inventory(repo.Repo, repo.Commit, _limits with { MaxTotalBytes = 100 }, TestContext.Current.CancellationToken);
        var over = await repo.Snapshot.Inventory(repo.Repo, repo.Commit, _limits with { MaxTotalBytes = 99 }, TestContext.Current.CancellationToken);

        Check(!atCap.IsError, "kept sizes exactly at the cap pass");
        Check(over.IsError && over.Error.Code == "SND-CODEIDX-003", "kept sizes one byte over the cap are SND-CODEIDX-003");
    }

    /// <summary>A path with a '..', '.git' (any case) or backslash component is skipped as unsafe-path and never kept.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Unsafe_paths_are_skipped_and_counted()
    {
        using var repo = Arrange(new RepoLayout().File("ok.txt", "fine").UnsafeFiles("bad", "..", ".GIT", "a\\b", "fine.txt"));

        var inventory = Ok(await repo.Snapshot.Inventory(repo.Repo, repo.Commit, _limits, TestContext.Current.CancellationToken));

        Check(inventory.Files.Select(f => f.Path).SequenceEqual(["bad/fine.txt", "ok.txt"], StringComparer.Ordinal), "only the safe paths are kept");
        Check(inventory.Skipped.GetValueOrDefault(SkipReason.UnsafePath) == 3, "the three unsafe paths are counted");
    }

    /// <summary>A commit the repo does not hold is SND-CODEIDX-002.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_commit_the_repo_lacks_is_SND_CODEIDX_002()
    {
        using var repo = Arrange(new RepoLayout().File("a.txt", "x"));

        var result = await repo.Snapshot.Inventory(repo.Repo, _unknownCommit, _limits, TestContext.Current.CancellationToken);

        Check(result.IsError && result.Error.Code == "SND-CODEIDX-002", "an unknown commit is SND-CODEIDX-002");
    }

    /// <summary>Blobs the store lacks are counted as missing, never fetched, and never an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Blobs_the_store_lacks_are_counted_as_missing_and_not_fetched()
    {
        using var repo = Arrange(new RepoLayout().File("a.txt", "one").File("b.txt", "two").Symlink("link").WithBlobsMissing());

        var inventory = Ok(await repo.Snapshot.Inventory(repo.Repo, repo.Commit, _limits, TestContext.Current.CancellationToken));

        Check(inventory.Files.Count == 0, "no file with a missing object is kept");
        Check(inventory.Skipped.GetValueOrDefault(SkipReason.Missing) == 2, "both missing files are counted");
        Check(inventory.Skipped.GetValueOrDefault(SkipReason.Symlink) == 1, "a symlink is still counted as one");
    }

    /// <summary>The reader answers each listed blob's bytes exactly, whatever they hold.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_reader_answers_each_blobs_exact_bytes()
    {
        string[] texts = ["", "one line\n", "\n\n", "no trailing newline", "tabs\tand\r\nCRLF\n", "ünïcödé ✓\n", "1 1 1\n"];
        var layout = new RepoLayout();
        for (var i = 0; i < texts.Length; i++)
        {
            _ = layout.File(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"f{i}.txt"), texts[i]);
        }

        using var repo = Arrange(layout);
        var inventory = Ok(await repo.Snapshot.Inventory(repo.Repo, repo.Commit, _limits, TestContext.Current.CancellationToken));
        await using var reader = Ok(await repo.Snapshot.OpenBlobs(repo.Repo, TestContext.Current.CancellationToken));

        foreach (var file in inventory.Files)
        {
            var expected = texts[int.Parse(file.Path[1..^4], System.Globalization.CultureInfo.InvariantCulture)];
            var bytes = Ok(await reader.Read(file.Blob, TestContext.Current.CancellationToken));
            Check(bytes is not null && Encoding.UTF8.GetString(bytes) == expected, $"{file.Path} reads back as it was committed");
        }
    }

    /// <summary>An object id the store lacks reads as null, and the reader keeps working afterwards.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_object_id_the_store_lacks_reads_as_null_and_the_reader_goes_on()
    {
        using var repo = Arrange(new RepoLayout().File("a.txt", "still here"));
        var inventory = Ok(await repo.Snapshot.Inventory(repo.Repo, repo.Commit, _limits, TestContext.Current.CancellationToken));
        await using var reader = Ok(await repo.Snapshot.OpenBlobs(repo.Repo, TestContext.Current.CancellationToken));

        var gone = Ok(await reader.Read("0123456789012345678901234567890123456789", TestContext.Current.CancellationToken));
        var after = Ok(await reader.Read(inventory.Files.Single().Blob, TestContext.Current.CancellationToken));

        Check(gone is null, "a lacking object reads as null");
        Check(after is not null && Encoding.UTF8.GetString(after) == "still here", "a read after a miss still answers");
    }

    private static void Check(bool condition, string clause)
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: {clause}");
        }
    }

    private static T Ok<T>(Kernel.Result<T> result)
        => result.IsError ? throw new ContractViolationException($"contract: expected a value, got {result.Error.Code}: {result.Error.Message}") : result.Value;
}
