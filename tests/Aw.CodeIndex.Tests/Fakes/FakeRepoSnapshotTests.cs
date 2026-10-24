using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Fakes;

public sealed class FakeRepoSnapshotTests
{
    private static readonly CommitSha _commit = CommitSha.From("0123456789abcdef0123456789abcdef01234567");
    private static readonly CodeIndexRepo _repo = new(RepoName.From("fixture"), "/nonexistent", CodeIndexSettings.Default);   // the fake never reads the path
    private static readonly SnapshotLimits _limits = new([], MaxFileBytes: 100, MaxTotalBytes: 1_000);

    [Fact, Trait("Lane", "unit")]
    public async Task The_inventory_keeps_regular_files_and_counts_symlinks_submodules_and_big_files()
    {
        var snapshot = new FakeRepoSnapshot()
            .WithFile(_commit, "src/A.cs", "class A {}")
            .WithFile(_commit, "big.bin", new string('x', 101))
            .WithSkipped(_commit, "link", SkipReason.Symlink)
            .WithSkipped(_commit, "module", SkipReason.Submodule);

        var inventory = (await snapshot.Inventory(_repo, _commit, _limits, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("src/A.cs", Assert.Single(inventory.Files).Path);
        Assert.Equal(1, inventory.Skipped[SkipReason.Symlink]);
        Assert.Equal(1, inventory.Skipped[SkipReason.Submodule]);
        Assert.Equal(1, inventory.Skipped[SkipReason.TooLarge]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Kept_files_over_the_total_cap_stop_with_SND_CODEIDX_003()
    {
        var snapshot = new FakeRepoSnapshot()
            .WithFile(_commit, "a.cs", new string('a', 60))
            .WithFile(_commit, "b.ts", new string('b', 60));

        var result = await snapshot.Inventory(_repo, _commit, _limits with { MaxTotalBytes = 100 }, TestContext.Current.CancellationToken);

        Assert.Equal("SND-CODEIDX-003", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_blob_is_skipped_as_missing_and_reads_as_null_and_a_present_one_reads_as_its_bytes()
    {
        var snapshot = new FakeRepoSnapshot()
            .WithFile(_commit, "present.txt", "hello")
            .WithFile(_commit, "gone.txt", "bye");
        var before = (await snapshot.Inventory(_repo, _commit, _limits, TestContext.Current.CancellationToken)).Value;
        var goneBlob = before.Files.Single(f => f.Path == "gone.txt").Blob;
        _ = snapshot.WithMissingBlob(_commit, "gone.txt");
        var inventory = (await snapshot.Inventory(_repo, _commit, _limits, TestContext.Current.CancellationToken)).Value;

        var reader = (await snapshot.OpenBlobs(_repo, TestContext.Current.CancellationToken)).Value;
        var present = await reader.Read(inventory.Files.Single(f => f.Path == "present.txt").Blob, TestContext.Current.CancellationToken);
        var gone = await reader.Read(goneBlob, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(inventory.Files, f => f.Path == "gone.txt");
        Assert.Equal(1, inventory.Skipped[SkipReason.Missing]);
        Assert.Equal("hello", Encoding.UTF8.GetString(present.Value!));
        Assert.Null(gone.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_commit_is_SND_CODEIDX_002()
    {
        var snapshot = new FakeRepoSnapshot();

        var result = await snapshot.Inventory(_repo, _commit, _limits, TestContext.Current.CancellationToken);

        Assert.Equal("SND-CODEIDX-002", result.Error.Code);
    }
}
