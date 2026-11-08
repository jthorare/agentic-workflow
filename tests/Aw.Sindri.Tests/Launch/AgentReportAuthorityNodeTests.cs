using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>Who may report a 'run_agent' node run: the launched session with its secret, never anyone else, and not while the node run is taken over.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentReportAuthorityNodeTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-00000000000000000000000000000001");

    private static FakeLaunchFiles Open()
    {
        var files = new FakeLaunchFiles();
        files.Open(_launch);
        return files;
    }

    [Fact]
    public void A_wrong_launch_secret_is_still_SND_AUTH_003_for_a_node_report()
    {
        var decided = AgentReportAuthority.DecideNode(_launch, takenOver: false, new LaunchSecret("not-the-secret"), Open());

        Assert.True(decided.IsError);
        Assert.Equal(SindriErrors.AuthLaunchSecret.Code, decided.Error.Code);
    }

    [Fact]
    public void The_launch_secret_reports_the_node_as_the_agent()
    {
        var decided = AgentReportAuthority.DecideNode(_launch, takenOver: false, new LaunchSecret(FakeLaunchFiles.DefaultProof), Open());

        Assert.Equal(StepRuntime.Agent, decided.Value);
    }

    [Fact]
    public void A_report_with_no_secret_is_SND_AUTH_002()
    {
        var decided = AgentReportAuthority.DecideNode(_launch, takenOver: false, secret: null, Open());

        Assert.Equal(SindriErrors.AuthRefused.Code, decided.Error.Code);
    }

    [Fact]
    public void A_taken_over_node_run_takes_a_report_from_the_human_only()
    {
        var decided = AgentReportAuthority.DecideNode(_launch, takenOver: true, new LaunchSecret(FakeLaunchFiles.DefaultProof), Open());

        Assert.Equal(SindriErrors.AuthRefused.Code, decided.Error.Code);
    }
}
