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

/// <summary>How an agent Step's target reads the stored 'fact:item' (#1135): a missing "spec" is no spec, and a fact that can't be read says why instead of "names no repo".</summary>
[Lane(TestLane.Unit)]
public sealed class AgentTargetResolverItemFactTests
{
    private const string ItemKeyText = "ITM-00000000000000000000000000000001";
    private static readonly FactValue _fact = new("fact:sha256:" + new string('a', 64), new string('a', 64), HeadSha: null);

    private static Task<Result<AgentStepTarget>> Resolve(string factJson) =>
        new AgentTargetResolver(new Approved(), new Contents(factJson), new ItemReposStub())
            .Resolve(_fact, InstanceAddress.From(ItemKeyText + "/t-parent/1"), TestContext.Current.CancellationToken).AsTask();

    [Theory]
    [InlineData("""{"id":"ITM-00000000000000000000000000000001","repo":"app"}""")]
    [InlineData("""{"id":"ITM-00000000000000000000000000000001","repo":"app","spec":null}""")]
    public async Task A_fact_item_without_a_spec_resolves_its_repo(string factJson)
    {
        var target = await Resolve(factJson);

        Assert.False(target.IsError, target.IsError ? target.Error.Message : null);
        Assert.Equal(("/work/app", ItemKeyText), (target.Value.RepoPath, target.Value.Item.Value));
    }

    [Theory]
    [InlineData("""{"id":7,"repo":"app"}""")]
    [InlineData("""{"repo":"app","spec":"s"}""")]
    public async Task A_fact_item_that_does_not_parse_reports_the_parse_error_not_a_missing_repo(string factJson)
    {
        var target = await Resolve(factJson);

        Assert.True(target.IsError);
        Assert.NotEqual(SindriErrors.LaunchRepoMissing.Code, target.Error.Code, StringComparer.Ordinal);
        Assert.Contains("id", target.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("names no repo", target.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fact_item_that_parses_but_names_no_repo_still_says_so()
    {
        var target = await Resolve("""{"id":"ITM-00000000000000000000000000000001","spec":"s"}""");

        Assert.True(target.IsError);
        Assert.Equal((SindriErrors.LaunchRepoMissing.Code, true), (target.Error.Code, target.Error.Message.Contains("names no repo", StringComparison.Ordinal)));
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
