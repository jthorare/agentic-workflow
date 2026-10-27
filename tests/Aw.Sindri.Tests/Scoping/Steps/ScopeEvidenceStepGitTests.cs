using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>'step.scope.evidence' over a real git checkout: the scope map's baseSha is the checkout's commit.</summary>
[Lane(TestLane.Integration)]
public sealed class ScopeEvidenceStepGitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_scope_packet_built_from_a_real_checkout_carries_its_head_commit_as_baseSha()
    {
        using var repo = await TempGitRepo.Create();
        var head = await repo.Git("rev-parse", "HEAD");
        var area = new InMemoryRunArea();
        var step = new Features.Scoping.ScopeEvidenceStep(area, new InMemoryPackets(), new InMemoryRepoCheckouts().With("web", repo.Path), new GitRunDiff(), new FakeScopeRunRecords());

        var result = await step.Run(ScopingStepContexts.Of("step.scope.evidence", ScopingFacts.Expiry().Without("rounds").Without("scopePacket")) with { Repo = RepoName.From("web") }, Token);

        Assert.Equal("complete", result.Result!.Outcome.Value);
        var map = JsonNode.Parse((await area.Read(ScopingStepContexts.Address, "scope-packet/scope-map.json", Token)).Value)!;
        Assert.Equal(head, (string?)map["baseSha"]);
    }
}
