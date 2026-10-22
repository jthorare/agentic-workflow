using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Prove;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Prove;

/// <summary>The git <c language="text">ICommitText</c> adapter against a real temp repo.</summary>
[Lane(TestLane.Integration)]
public sealed class GitCommitTextTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(string Base, string Head)> Commit(TempGitRepo repo)
    {
        var baseSha = await repo.Git("rev-parse", "HEAD");
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "a.txt"), "patch line carrying the token\n", Token);
        _ = await repo.Git("add", "a.txt");
        _ = await repo.Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "-m", "message line carrying the token");
        return (baseSha, await repo.Git("rev-parse", "HEAD"));
    }

    [Fact]
    public async Task The_range_holds_the_messages_and_the_patches_after_the_base_up_to_the_head()
    {
        using var repo = await TempGitRepo.Create();
        var (baseSha, head) = await Commit(repo);

        var text = await new GitCommitText().Range(repo.Path, baseSha, head, Token);

        Assert.True(text.IsSuccess);
        Assert.Contains("message line carrying the token", text.Value, StringComparison.Ordinal);
        Assert.Contains("+patch line carrying the token", text.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_range_is_an_empty_text()
    {
        using var repo = await TempGitRepo.Create();
        var head = await repo.Git("rev-parse", "HEAD");

        var text = await new GitCommitText().Range(repo.Path, head, head, Token);

        Assert.Equal(string.Empty, text.Value);
    }

    [Theory]
    [InlineData("--output=/tmp/x", "abc")]
    [InlineData("abc", "HEAD")]
    [InlineData("", "abc")]
    public async Task A_sha_that_is_not_hex_is_refused_before_git_runs(string baseSha, string head)
    {
        using var repo = await TempGitRepo.Create();

        var text = await new GitCommitText().Range(repo.Path, baseSha, head, Token);

        Assert.True(text.IsError);
    }

    [Fact]
    public async Task A_commit_the_checkout_does_not_hold_is_an_error()
    {
        using var repo = await TempGitRepo.Create();
        var head = await repo.Git("rev-parse", "HEAD");

        var text = await new GitCommitText().Range(repo.Path, "deadbeef", head, Token);

        Assert.True(text.IsError);
    }
}
