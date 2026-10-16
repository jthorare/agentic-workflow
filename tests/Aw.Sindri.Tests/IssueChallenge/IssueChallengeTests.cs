using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApproveRecipe;

namespace Aw.Sindri.Tests.IssueChallenge;

/// <summary>
/// Issuing a challenge (engine spec §4.2, API spec §4.1 'issue'): the server parses the action, plans it and returns
/// the challenge with its preview, to a human at a terminal only. S1-D5: 'approve profile &lt;hash6&gt;' too.
/// </summary>
public sealed class IssueChallengeTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Approve_action_gets_a_five_minute_challenge_with_the_preview()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var issued = (await r.Issue("approve workflow.step 1")).Value;
        var byHash = (await r.Issue($"approve workflow.step@{step.Hash.Display}")).Value;

        Assert.Equal(("approve workflow.step 1", r.H.Clock.GetUtcNow().AddMinutes(5)), (issued.Action, issued.ExpiresAt));
        Assert.Equal((step.Id, step.Hash), (issued.Approve!.Id, issued.Approve.Hash));   // why: an approval's challenge carries its preview
        Assert.Null(issued.Revoke);
        Assert.Equal("approve workflow.step 1", byHash.Action);
        Assert.NotEqual(issued.Challenge, byHash.Challenge);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("approve workflow.step 2", "SND-RECIPE-002")]
    [InlineData("approve workflow.nothing 1", "SND-RECIPE-002")]
    [InlineData("approve workflow.step", "SND-CLI-003")]
    [InlineData("grant 1 T-1/step/1", "SND-WF-001")]
    [InlineData("revoke workflow.step@zzzz", "SND-CLI-003")]
    public async Task Unknown_actions_and_recipes_are_refused(string action, string code)
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        _ = await r.Shipped("sindri/workflow.step.yaml");

        Assert.Equal(code, (await r.Issue(action)).Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("session")]
    [InlineData("app")]
    public async Task Only_a_human_at_a_terminal_gets_a_challenge(string who)
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        _ = await r.Shipped("sindri/workflow.step.yaml");
        var caller = who == "session" ? A.Caller().Session().Build() : A.Caller().Human().Client(ClientLabel.Ui).Build();

        var refused = await r.Issue("approve workflow.step 1", caller);

        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.Equal("IssueChallenge is a human decision. Ask the human to run it in a terminal: sindri recipe approve <recipe>", refused.Error.Message);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("revoke workflow.step@9ab2d40c1e77", "sindri recipe revoke <recipe>", "sindri recipe approve")]
    [InlineData("approve workflow.step 1", "sindri recipe approve <recipe>", "sindri recipe revoke")]
    public async Task A_session_is_refused_with_the_one_command_for_its_action(string action, string named, string other)
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        _ = await r.Shipped("sindri/workflow.step.yaml");

        var refused = await r.Issue(action, A.Caller().Session().Build());

        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.EndsWith(named, refused.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(other, refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Profile_approval_gets_a_challenge_bound_to_the_live_hash()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        r.H.Profiles.Live = SliceHarness.Profile();
        var hash = r.H.Profiles.Live.Value.Hash;

        var issued = (await r.Issue($"approve profile {hash.Abbreviated}")).Value;
        var other = await r.Issue("approve profile 000000");

        Assert.Equal("approve profile " + hash.Abbreviated, issued.Action);
        Assert.Null(issued.Approve);
        Assert.Null(issued.Revoke);
        Assert.Equal(SindriErrors.ProfileHashMismatch.Code, other.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revoke_by_a_hash_prefix_names_the_twelve_digit_form()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(step);

        var issued = (await r.Issue($"revoke workflow.step@{step.Hash.Digest[..8]}")).Value;

        Assert.Equal($"revoke workflow.step@{step.Hash.Display}", issued.Action);
        Assert.Equal(step.Hash, issued.Revoke!.Hash);   // why: a revoke's challenge carries its preview
        Assert.Equal(WorkflowErrors.RecipeNotFound.Code, (await r.Issue("revoke workflow.step@0123456789ab")).Error.Code);
    }
}
