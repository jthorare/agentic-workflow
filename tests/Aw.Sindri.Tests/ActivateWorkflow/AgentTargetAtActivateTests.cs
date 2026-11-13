using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Graph.Blocks;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.Graph.GraphFixtures;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.ActivateWorkflow;

/// <summary>
/// A 'run_agent' node with no 'agent' runs on the profile's 'workflow.agent.target' (AgentNodeLaunching.AgentOf). With neither, the run fails at launch (SND-LAUNCH-003),
/// so activate and 'wf validate --slug' refuse it first (#2986). The validator is the one ProfileWorkflowChecker builds from the profile.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AgentTargetAtActivateTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new(realBlocks: true);

    public void Dispose() => _rig.Dispose();

    private static GraphSpec AgentGraph(string? agent)
    {
        var config = new JsonObject { ["repo"] = "web", ["prompt"] = "check" };
        if (agent is not null)
        {
            config["agent"] = agent;
        }

        return Spec([Trigger("daily"), new GraphNode(NodeId.From("a"), "run_agent", Title: null, config)], [Edge("daily", "a")]);
    }

    private static ProfileWorkflowChecker Checker(string? target)
    {
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/home/web")],
            Workflow: target is null ? null : new WorkflowSettings(Agent: new WorkflowAgentSettings(Target: AgentId.From(target))));
        var profiles = new InMemoryProfiles { Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml") };
        return new ProfileWorkflowChecker(profiles, GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}")));
    }

    private async Task<int> StoreRevision(GraphSpec spec)
    {
        await _rig.Seed();
        return (await _rig.Workflows.Save(Id, 1, spec, "owner", Token)).Value.Revision;
    }

    [Fact]
    public async Task Activate_refuses_a_node_with_no_agent_when_the_profile_has_no_target()
    {
        var revision = await StoreRevision(AgentGraph(agent: null));
        var service = new ActivateWorkflowService(_rig.Workflows, Checker(target: null), _rig.Log, _rig.Harness.Callers);

        var result = await service.Handle(new ActivateWorkflowRequest(Slug, revision, "owner"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphFindings.Code, result.Error.Code);
        Assert.Contains("'a'", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("workflow.agent.target", result.Error.Message, StringComparison.Ordinal);
        Assert.Null((await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
    }

    [Fact]
    public async Task Validate_by_slug_reports_the_same_blocking_finding()
    {
        var revision = await StoreRevision(AgentGraph(agent: null));
        var service = new ValidateWorkflowService(_rig.Workflows, Checker(target: null));

        var result = await service.Handle(new ValidateWorkflowRequest(Slug, revision, Spec: null), Token);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Passed);
        var finding = Assert.Single(result.Value.Findings, static f => f.Severity == "error");
        Assert.Contains("a", finding.Nodes, StringComparer.Ordinal);
        Assert.Contains("workflow.agent.target", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Activate_passes_when_the_node_names_its_own_agent()
    {
        var revision = await StoreRevision(AgentGraph("codex"));
        var service = new ActivateWorkflowService(_rig.Workflows, Checker(target: null), _rig.Log, _rig.Harness.Callers);

        var result = await service.Handle(new ActivateWorkflowRequest(Slug, revision, "owner"), Token);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Activate_passes_when_the_profile_has_a_target()
    {
        var revision = await StoreRevision(AgentGraph(agent: null));
        var service = new ActivateWorkflowService(_rig.Workflows, Checker(target: "claude-code"), _rig.Log, _rig.Harness.Callers);

        var result = await service.Handle(new ActivateWorkflowRequest(Slug, revision, "owner"), Token);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_draft_save_of_the_node_with_no_agent_still_passes()
    {
        var validator = await Checker(target: null).Validator(Token);

        Assert.True(validator.Validate(AgentGraph(agent: null), GraphMode.Draft).Passed);
    }
}
