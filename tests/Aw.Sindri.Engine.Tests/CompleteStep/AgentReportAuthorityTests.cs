using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>
/// The one gate that survives opening every verb to agent sessions (PR 2 of the agent-first spec): a report on an agent run still has to carry
/// that launch's own secret. A session that opens every other verb gets nothing here by being a session.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AgentReportAuthorityTests
{
    private const string Proof = "the-launch-secret";
    private static readonly InstanceAddress _address = InstanceAddress.From("T-9/step/1");
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('c', 32));

    [Theory]
    [InlineData("not-the-secret")]
    [InlineData("")]
    [InlineData("the-launch-secret ")]
    public void A_wrong_launch_secret_is_still_SND_AUTH_003(string presented)
    {
        var files = new FakeLaunchFiles();
        files.Open(_launch, Proof);

        var decided = AgentReportAuthority.Decide(AgentRunView(), Request(new LaunchSecret(presented)), files);

        Assert.True(decided.IsError);
        Assert.Equal("SND-AUTH-003", decided.Error.Code);
    }

    [Fact]
    public void The_matching_launch_secret_reports_the_run_as_the_agent()
    {
        var files = new FakeLaunchFiles();
        files.Open(_launch, Proof);

        var decided = AgentReportAuthority.Decide(AgentRunView(), Request(new LaunchSecret(Proof)), files);

        Assert.False(decided.IsError, decided.IsError ? decided.Error.Message : null);
        Assert.Equal(StepRuntime.Agent, decided.Value);
    }

    [Fact]
    public void A_session_without_the_secret_is_still_refused_an_agent_run()
    {
        var files = new FakeLaunchFiles();
        files.Open(_launch, Proof);

        var decided = AgentReportAuthority.Decide(AgentRunView(), Request(secret: null), files);

        Assert.Equal("SND-AUTH-002", decided.Error.Code);
    }

    private static InstanceView AgentRunView() =>
        new(_address, new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64))), "produce",
            DateTimeOffset.FromUnixTimeMilliseconds(1_000))
        {
            Id = WorkflowInstanceId.Of(_address),
            Version = 3,
            AgentRun = new OpenRun(RunToken.For(1, 1), StepRuntime.Agent, _launch.Value, Task: null),
        };

    private static CompleteStepRequest Request(LaunchSecret? secret) =>
        new(_address.Value, RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce, secret);
}
