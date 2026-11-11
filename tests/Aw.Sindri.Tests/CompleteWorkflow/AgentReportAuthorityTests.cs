using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.CompleteWorkflow;

/// <summary>Who may report a 'run_agent' node (PR 7): the contract of the Step report, kept for node runs.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentReportAuthorityTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-0192f0c1a2b37c4d8e9f00112233aabb");

    private static CompleteStepRequest Report(LaunchSecret? proof) =>
        new("walk@1:1", RunToken.For(2, 1), OutcomeName.From("done"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), Nonce: null, proof);

    private static FakeLaunchFiles Files()
    {
        var files = new FakeLaunchFiles();
        files.Open(_launch);
        return files;
    }

    [Fact]
    public void A_wrong_launch_secret_is_still_SND_AUTH_003_for_a_node_report()
    {
        var decided = AgentReportAuthority.DecideNode(new NodeReportView(_launch, TakenOver: false), Report(new LaunchSecret("not-the-one")), Files());

        Assert.True(decided.IsError);
        Assert.Equal(SindriErrors.AuthLaunchSecret.Code, decided.Error.Code);
    }

    [Fact]
    public void The_right_secret_is_the_agent_and_a_report_with_none_is_refused()
    {
        var agent = AgentReportAuthority.DecideNode(new NodeReportView(_launch, TakenOver: false), Report(new LaunchSecret(FakeLaunchFiles.DefaultProof)), Files());
        var bare = AgentReportAuthority.DecideNode(new NodeReportView(_launch, TakenOver: false), Report(proof: null), Files());

        Assert.Equal(StepRuntime.Agent, agent.Value);
        Assert.True(bare.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, bare.Error.Code);
    }

    [Fact]
    public void A_taken_over_node_is_reported_by_the_human_only()
    {
        var agent = AgentReportAuthority.DecideNode(new NodeReportView(_launch, TakenOver: true), Report(new LaunchSecret(FakeLaunchFiles.DefaultProof)), Files());
        var human = AgentReportAuthority.DecideNode(new NodeReportView(_launch, TakenOver: true), Report(proof: null), Files());

        Assert.True(agent.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, agent.Error.Code);
        Assert.Equal(StepRuntime.Desk, human.Value);
    }

    [Fact]
    public void A_secret_for_a_report_that_names_no_running_agent_node_is_refused()
    {
        var withSecret = AgentReportAuthority.DecideNode(view: null, Report(new LaunchSecret(FakeLaunchFiles.DefaultProof)), Files());
        var desk = AgentReportAuthority.DecideNode(view: null, Report(proof: null), Files());

        Assert.True(withSecret.IsError);
        Assert.Equal(StepRuntime.Desk, desk.Value);
    }
}
