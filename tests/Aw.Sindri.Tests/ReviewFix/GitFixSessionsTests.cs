using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Reviews;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>Matching a fix task to a live session (PR review page spec §8.2 step 3): the checkout's origin is the head repo AND its branch is the head branch.</summary>
[Lane(TestLane.Unit)]
public sealed class GitFixSessionsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ObservedSession Session(string id, string cwd, LaunchId? launch = null) =>
        new(new SessionRef(AgentId.From("claude-code"), SessionId.From(id)), 1, 1, SessionState.Idle, StateSource.Record, KnownInstants.Epoch) { Cwd = cwd, LaunchId = launch };

    private static GitFixSessions Sessions(IReadOnlyList<ObservedSession> live, string origin, string branch)
    {
        var snapshot = new SessionSnapshotStore();
        snapshot.Publish(new SessionSnapshot(live, [], [], KnownInstants.Epoch));
        var runner = new FakeToolRunner((_, args) => args.Contains("get-url", StringComparer.Ordinal) ? new ToolRun(ToolOutcome.Exited, 0, origin + "\n") : new ToolRun(ToolOutcome.Exited, 0, branch + "\n"));
        return new GitFixSessions(snapshot, runner);
    }

    [Fact]
    public async Task A_session_on_the_head_repo_and_branch_matches()
    {
        var sessions = Sessions([Session("aaaaaaaa-0000-4000-8000-000000000001", "/work/api")], "git@github.com:Sam-Dev/api.git", "feat/retry");

        Assert.Equal("claude-code, /work/api", await sessions.OnBranch("sam-dev/api", "feat/retry", Token));
    }

    [Fact]
    public async Task A_fork_s_main_matches_nothing_when_the_origin_is_another_repo()
    {
        var sessions = Sessions([Session("aaaaaaaa-0000-4000-8000-000000000001", "/work/other")], "https://github.com/acme/other.git", "main");

        Assert.Null(await sessions.OnBranch("sam-dev/api", "main", Token));
    }

    [Fact]
    public async Task The_same_repo_on_another_branch_matches_nothing()
    {
        var sessions = Sessions([Session("aaaaaaaa-0000-4000-8000-000000000001", "/work/api")], "https://github.com/sam-dev/api.git", "main");

        Assert.Null(await sessions.OnBranch("sam-dev/api", "feat/retry", Token));
    }

    [Fact]
    public async Task A_session_Sindri_launched_is_skipped()
    {
        var sessions = Sessions([Session("aaaaaaaa-0000-4000-8000-000000000001", "/work/api", A.FirstLaunchId)], "https://github.com/sam-dev/api.git", "feat/retry");

        Assert.Null(await sessions.OnBranch("sam-dev/api", "feat/retry", Token));
    }
}
