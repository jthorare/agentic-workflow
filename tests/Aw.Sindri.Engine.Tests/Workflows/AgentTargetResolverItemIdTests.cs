using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The id in a stored 'fact:item' (#1135): one that isn't an item id says so instead of blaming the repo.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentTargetResolverItemIdTests
{
    private const string ItemKeyText = "ITM-00000000000000000000000000000001";
    private static readonly FactValue _fact = new("fact:sha256:" + new string('a', 64), new string('a', 64), HeadSha: null);

    private static Task<Result<AgentStepTarget>> Resolve(string factJson) =>
        new AgentTargetResolver(new Approved(), new Contents(factJson), new ItemReposStub())
            .Resolve(_fact, InstanceAddress.From(ItemKeyText + "/t-parent/1"), TestContext.Current.CancellationToken).AsTask();

    [Theory]
    [InlineData("""{"id":"ITM-1","repo":"app"}""")]
    [InlineData("""{"id":"","repo":"app"}""")]
    public async Task A_fact_item_whose_id_is_not_an_item_id_says_so_not_that_the_repo_is_unlisted(string factJson)
    {
        var target = await Resolve(factJson);

        Assert.True(target.IsError);
        Assert.NotEqual(SindriErrors.LaunchRepoMissing.Code, target.Error.Code, StringComparer.Ordinal);
        Assert.Contains("item id", target.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("approved profile", target.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_valid_item_id_with_a_repo_the_profile_lacks_still_reports_the_unlisted_repo()
    {
        var target = await Resolve("""{"id":"ITM-00000000000000000000000000000001","repo":"other"}""");

        Assert.True(target.IsError);
        Assert.Equal((SindriErrors.LaunchRepoMissing.Code, true), (target.Error.Code, target.Error.Message.Contains("approved profile", StringComparison.Ordinal)));
    }

    private sealed class Approved : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            var declared = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Repos: [new RepoEntry("app", "/work/app")],
                Workflow: new WorkflowSettings(Agent: new WorkflowAgentSettings(AgentId.From("claude-code"))));
            var profile = ProfileRules.Check(declared).Value;
            return ValueTask.FromResult<Result<LoadedProfile?>>(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"));
        }
    }

    private sealed class Contents(string json) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ReadOnlyMemory<byte>>>(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json)));
    }
}
