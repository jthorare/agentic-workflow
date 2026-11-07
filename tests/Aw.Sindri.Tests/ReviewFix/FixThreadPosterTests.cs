using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Features.SendReviewFix;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>Posting a fix thread (PR review page spec §8.2 step 2): a failure is the half-done answer with SND-REVIEW-006, and a retry with the same marker posts once.</summary>
[Lane(TestLane.Unit)]
public sealed class FixThreadPosterTests
{
    private static readonly string _sha = new('a', 40);
    private readonly FakePullRequests _forge = new(["github.com"]);
    private readonly FakePull _pull;

    public FixThreadPosterTests()
    {
        _pull = _forge.Add(new PullRepo("github.com", "acme", "api"), "feat/retry", _sha);
        _pull.Files = [new PullFile("src/Sync.cs", PreviousPath: null, PullFileStatus.Modified, 1, 0, "@@ -40,0 +41,3 @@\n+a\n+b\n+c")];
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private PullReview Review(string head, string marker = "sindri.fix:ITM-1", int line = 42) =>
        new(_pull.Link, head, PullReviewVerdict.Comment, string.Empty, [new PullReviewComment("src/Sync.cs", line, "Guard the null.")], marker);

    [Fact]
    public async Task A_posted_thread_answers_posted_with_its_link()
    {
        var posted = await new FixThreadPoster(_forge).Post(Review(_sha), "ITM-1", 1, Token);

        Assert.True(posted.Posted);
        Assert.Null(posted.Code);
        Assert.StartsWith(_pull.Link + "#", posted.Thread, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_head_that_moved_is_the_half_done_answer_and_posts_nothing()
    {
        var posted = await new FixThreadPoster(_forge).Post(Review(new string('b', 40)), "ITM-1", 1, Token);

        Assert.False(posted.Posted);
        Expect.Equal("SND-REVIEW-006", posted.Code);
        Assert.StartsWith("Item ITM-1 created in Work; posting to #1 failed: ", posted.Error, StringComparison.Ordinal);
        Assert.Empty(_pull.Threads);
    }

    [Fact]
    public async Task A_line_the_forge_refuses_is_the_half_done_answer()
    {
        var posted = await new FixThreadPoster(_forge).Post(Review(_sha, line: 999), "ITM-1", 1, Token);

        Assert.False(posted.Posted);
        Expect.Equal("SND-REVIEW-006", posted.Code);
    }

    [Fact]
    public async Task A_retry_with_the_same_marker_posts_one_thread()
    {
        var poster = new FixThreadPoster(_forge);

        _ = await poster.Post(Review(_sha), "ITM-1", 1, Token);
        var again = await poster.Post(Review(_sha), "ITM-1", 1, Token);

        Assert.True(again.Posted);
        _ = Assert.Single(_pull.Threads);
    }
}
