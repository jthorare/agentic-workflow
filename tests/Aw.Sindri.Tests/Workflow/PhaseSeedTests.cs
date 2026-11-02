using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Prepare;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Worktrees;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>A phase workspace's base commit for the host-side prepare is the commit it was seeded at (sandbox spec §3.8), never the HEAD of the worktree an earlier Step's agent has been committing in.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseSeedTests
{
    private const string Seed = "0123456789abcdef0123456789abcdef01234567";
    private const string Checkout = "/repos/dev";

    [Fact]
    public void A_phase_workspace_launch_uses_the_seed_commit_not_HEAD()
    {
        Expect.Equal(Seed, PhaseSeed.BaseOf(LineContexts.Of(Seed)));
        Expect.True(PhaseSeed.BaseOf(LineContexts.Of("HEAD")) is null);   // not a full sha: no base, and the prepare is skipped
        Expect.True(PhaseSeed.BaseOf("not json") is null);
        Expect.True(PhaseSeed.BaseOf("""{"line":"l1"}""") is null);
    }

    [Fact]
    public async Task PhaseSeed_reads_the_same_base_the_seed_Step_seeds_from()
    {
        var lineContext = LineContexts.Of(Seed);
        var worktrees = new FakeWorktrees().WithRepo(Checkout, Seed);
        var step = new PhaseSeedStep(worktrees, new InMemoryRepoCheckouts().With("name", Checkout));
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["phasePacket"] = """{"hash":"sha256:aa","dir":"/packets/p2"}""", ["lineContext"] = lineContext };

        var result = await step.Run(BuildShipContexts.Of("step.phase.seed", facts) with { Repo = RepoName.From("name") }, TestContext.Current.CancellationToken);

        Expect.Equal("seeded", result.Result!.Outcome.Value);
        Expect.Equal(Expect.Only(worktrees.Requests).BaseCommit, PhaseSeed.BaseOf(lineContext));   // the commit the Step seeded the worktree at
    }
}
