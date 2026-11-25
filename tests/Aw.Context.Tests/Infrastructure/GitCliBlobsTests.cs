using Aw.Context.Infrastructure.Git;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>#3085: many blobs from one 'git cat-file --batch' process.</summary>
[Trait("Lane", "integration")]
public sealed class GitCliBlobsTests
{
    [Fact]
    public async Task Blobs_reads_every_blob_in_one_call_and_a_missing_one_is_null()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.md"] = "alpha\nline two\n",
            ["b.md"] = "﻿beta ünï\r\n",
            ["empty.md"] = string.Empty,
        });
        var a = repo.Run("rev-parse", "HEAD:a.md");
        var b = repo.Run("rev-parse", "HEAD:b.md");
        var empty = repo.Run("rev-parse", "HEAD:empty.md");
        const string missing = "0123456789abcdef0123456789abcdef01234567";

        var result = await new GitCli().Blobs(repo.Path, [a, missing, b, empty, a, "not-a-sha"], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("alpha\nline two\n", result.Value[a]);
        Assert.Equal("beta ünï\r\n", result.Value[b]);   // the BOM goes, as it does for Blob
        Assert.Equal(string.Empty, result.Value[empty]);
        Assert.Null(result.Value[missing]);
        Assert.Null(result.Value["not-a-sha"]);
        Assert.Equal((await new GitCli().Blob(repo.Path, b, TestContext.Current.CancellationToken)).Value, result.Value[b]);
    }

    [Fact]
    public async Task Blobs_of_a_path_that_is_not_a_repo_is_SND_CONTEXT_010()
    {
        var result = await new GitCli().Blobs(Path.Combine(Path.GetTempPath(), "aw-ctx-no-such-repo"), ["0123456789abcdef0123456789abcdef01234567"], TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("SND-CONTEXT-010", result.Error.Code);
    }
}
