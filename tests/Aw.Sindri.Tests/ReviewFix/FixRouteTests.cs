using Aw.Kernel.Testing;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.SendReviewFix;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>Who takes a fix task (PR review page spec §8.2 step 3): a babysitter, else a session on the head, else it waits.</summary>
[Lane(TestLane.Unit)]
public sealed class FixRouteTests
{
    private const string Link = "https://github.com/acme/api/pull/2071";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_babysitter_on_the_pull_request_takes_it_before_any_session()
    {
        var route = new FixRoute(new Babysitters(watching: true), new Sessions("claude-code, ~/repos/api"));

        Expect.Equal(("babysitter", null), await route.Find(Link, "sam-dev/api", "feat/retry", Token));
    }

    [Fact]
    public async Task A_session_on_the_head_takes_it_when_no_babysitter_does()
    {
        var route = new FixRoute(new Babysitters(watching: false), new Sessions("claude-code, ~/repos/api"));

        Expect.Equal(("session", "claude-code, ~/repos/api"), await route.Find(Link, "sam-dev/api", "feat/retry", Token));
    }

    [Fact]
    public async Task With_neither_the_task_waits()
    {
        var route = new FixRoute(new Babysitters(watching: false), new Sessions(session: null));

        Expect.Equal(("waiting", null), await route.Find(Link, "sam-dev/api", "feat/retry", Token));
    }

    private sealed class Babysitters(bool watching) : IFixBabysitters
    {
        public ValueTask<bool> Watching(string link, CancellationToken cancellationToken) => ValueTask.FromResult(watching);
    }

    private sealed class Sessions(string? session) : IFixSessions
    {
        public ValueTask<string?> OnBranch(string headRepo, string branch, CancellationToken cancellationToken) => ValueTask.FromResult(session);
    }
}
