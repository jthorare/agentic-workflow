using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Host.Forge;

namespace Aw.Sindri.Tests.Forge;

[Lane(TestLane.Unit)]
public sealed class NoForgePullRequestsTests
{
    private const string Link = "https://github.com/acme/widgets/pull/1";
    private static readonly PullRepo _repo = new("github.com", "acme", "widgets");

    [Fact]
    public async Task Every_call_answers_no_forge_and_none_throws()
    {
        NoForgePullRequests pulls = new();
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal("SND-FORGE-001", Code(await pulls.Get(Link, ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.CheckLog(Link, "build", ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.Push(new PullPush(Link, "/w", "a", "b"), ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.RerunChecks(Link, "a", ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.Find(new FindPull(_repo, "feature/x", Marker: null), ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.Open(new OpenPull(_repo, "feature/x", "main", "a", "T", "B", Draft: false, Marker: null), ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.Self(ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.Threads(Link, ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.Merge(new PullMerge(Link, PullMergeMethod.Squash, "a"), ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.Reply(new PullReply(Link, "pull", "t", "m"), ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.React(new PullReaction(Link, "k", "+1", "m"), ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.PushBranch(new PullBranchPush(_repo, "/w", "feature/x", "a", LeaseSha: null), ct)));
        Assert.Equal("SND-FORGE-001", Code(await pulls.Update(Link, "T", body: null, ct)));
    }

    private static string Code<T>(Result<T> result) => result.IsError ? result.Error.Code : "success";
}
