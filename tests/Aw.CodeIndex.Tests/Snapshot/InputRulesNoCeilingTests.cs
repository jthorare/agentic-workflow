using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Snapshot;

/// <summary>A big repo gets an index: no whole-build failure on file count, and only code files count toward the size (removed per owner q93, 2026-10-23, CIX-L07 and CIX-L08).</summary>
public sealed class InputRulesNoCeilingTests
{
    private static readonly CommitSha _commit = CommitSha.From("0123456789abcdef0123456789abcdef01234567");
    private static readonly SnapshotLimits _limits = new([], MaxFileBytes: 10_000, MaxTotalBytes: 100);

    private static TreeEntry Regular(string path, string blob, long size) => new("100644", path, blob, size);

    [Fact, Trait("Lane", "unit")]
    public void More_kept_files_than_the_old_file_ceiling_still_build()
    {
        var entries = Enumerable.Range(0, 10).Select(i => Regular(string.Create(CultureInfo.InvariantCulture, $"f{i}.txt"), string.Create(CultureInfo.InvariantCulture, $"b{i}"), 1)).ToList();

        var result = InputRules.Apply(_commit, entries, _limits);

        Assert.False(result.IsError);
        Assert.Equal(10, result.Value.Files.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_code_files_count_toward_the_total_and_code_over_it_still_stops_the_build()
    {
        var docsAndAssets = InputRules.Apply(_commit, [Regular("a.cs", "1", 60), Regular("docs/big.md", "2", 5_000), Regular("logo.png", "3", 5_000)], _limits);
        var codeOver = InputRules.Apply(_commit, [Regular("a.cs", "1", 60), Regular("b.ts", "2", 41)], _limits);

        Assert.False(docsAndAssets.IsError);
        Assert.Equal("SND-CODEIDX-003", codeOver.Error.Code);
    }
}
