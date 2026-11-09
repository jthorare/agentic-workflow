using Aw.Kernel.Testing;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.SendReviewFix;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>Who takes a fix task (PR review page spec §8.2 step 3): a session on the head, else it waits.</summary>
[Lane(TestLane.Unit)]
public sealed class FixRouteTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_session_on_the_head_takes_it()
    {
        var route = new FixRoute(new Sessions("claude-code, ~/repos/api"));

        Expect.Equal(("session", "claude-code, ~/repos/api"), await route.Find("sam-dev/api", "feat/retry", Token));
    }

    [Fact]
    public async Task With_no_session_the_task_waits()
    {
        var route = new FixRoute(new Sessions(session: null));

        Expect.Equal(("waiting", null), await route.Find("sam-dev/api", "feat/retry", Token));
    }

    [Fact]
    public async Task An_unknown_head_matches_no_session() =>
        Expect.Equal(("waiting", null), await new FixRoute(new Sessions("claude-code, ~/x")).Find(headRepo: null, branch: null, Token));

    private sealed class Sessions(string? session) : IFixSessions
    {
        public ValueTask<string?> OnBranch(string headRepo, string branch, CancellationToken cancellationToken) => ValueTask.FromResult(session);
    }
}
