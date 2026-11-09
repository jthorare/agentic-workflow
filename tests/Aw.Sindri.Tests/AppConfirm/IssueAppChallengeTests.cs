using Aw.Kernel.Testing;
using Aw.Sindri.Features.IssueChallenge;

namespace Aw.Sindri.Tests.AppConfirm;

/// <summary>Issuing the app's challenge (setup-once spec §4 items 1 and 2): no action form is left (the recipe ones went with the engine, the profile one with the approval), so every text is refused.</summary>
[Lane(TestLane.Unit)]
public sealed class IssueAppChallengeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("approve profile 0a1b2c")]
    [InlineData("approve-all 0a1b2c")]
    [InlineData("approve workflow.step 3")]
    [InlineData("revoke workflow.step@9ab2d40c1e77")]
    [InlineData("grant 1 T-1/step/1")]
    [InlineData("accept T-1/step/1")]
    [InlineData("stop")]
    [InlineData("")]
    public async Task Every_action_is_refused_as_a_human_decision(string action)
    {
        var issued = await new IssueAppChallengeService().Handle(new IssueAppChallengeRequest(action), Token);

        Assert.Equal("SND-AUTH-002", issued.Error.Code);
    }

    [Theory]
    [InlineData("approve profile 0a1b2c")]
    [InlineData("approve-all 0a1b2c")]
    public async Task The_terminal_route_issues_no_challenge_either(string action)
    {
        var issued = await new IssueChallengeService().Handle(new IssueChallengeRequest(action), Token);

        Assert.Equal("SND-CLI-003", issued.Error.Code);
    }
}
