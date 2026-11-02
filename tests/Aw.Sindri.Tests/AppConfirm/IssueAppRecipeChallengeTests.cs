using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApproveRecipe;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.AppConfirm;

/// <summary>The app's challenge for one recipe approval, through the real command chain and the Host's catalog: the human token and a session both get it (owner decision q93).</summary>
[Lane(TestLane.Unit)]
public sealed class IssueAppRecipeChallengeTests
{
    private static readonly CallerContext _app = A.Caller().Human().Client(ClientLabel.Ui).Build();

    private static RegistryHarness Harness() => new(static services => services
        .AddScoped<IValidator<IssueAppChallengeRequest>, AcceptAllValidator<IssueAppChallengeRequest>>()
        .AddCommand<IssueAppChallengeRequest, IssueChallengeResponse, IssueAppChallengeService>());

    [Fact]
    public async Task A_recipe_approval_is_confirmed_by_typing_and_carries_its_preview()
    {
        await using var registry = Harness();
        _ = await registry.H.HoldLease();
        _ = await registry.Shipped("sindri/workflow.step.yaml");

        var issued = await registry.Run<IssueAppChallengeRequest, IssueChallengeResponse>(new IssueAppChallengeRequest("approve workflow.step 5"), _app);

        Assert.True(issued.IsSuccess, issued.IsError ? issued.Error.ToString() : null);
        Assert.Equal(("approve workflow.step 5", "typed", null), (issued.Value.Action, issued.Value.Confirm, issued.Value.TypedAlternative));
        Assert.NotNull(issued.Value.Approve);
    }

    [Fact]
    public async Task A_session_is_let_through_like_a_human()
    {
        await using var registry = Harness();
        _ = await registry.H.HoldLease();
        _ = await registry.Shipped("sindri/workflow.step.yaml");

        var issued = await registry.Run<IssueAppChallengeRequest, IssueChallengeResponse>(new IssueAppChallengeRequest("approve workflow.step 5"), A.Caller().Session().Build());

        Assert.True(issued.IsSuccess, issued.IsError ? issued.Error.ToString() : null);
    }

    [Fact]
    public async Task A_service_is_refused_before_the_handler()
    {
        await using var registry = Harness();
        _ = await registry.H.HoldLease();   // why: the refusal's audit row needs the lease

        var issued = await registry.Run<IssueAppChallengeRequest, IssueChallengeResponse>(new IssueAppChallengeRequest("approve workflow.step 5"), A.Caller().Service("jobs").Build());

        Assert.Equal("SND-AUTH-002", issued.Error.Code);
    }
}
