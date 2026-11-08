using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ApplyCommand;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>The action classes of the commands (engine spec §6.2, W22) and the action text of a protected answer's challenge (§4.2).</summary>
[Lane(TestLane.Unit)]
public sealed class ApplyCommandChallengeTests
{
    [Theory]
    [InlineData(AnswerVariant.Grant)]
    [InlineData(AnswerVariant.GrantTokens)]
    [InlineData(AnswerVariant.Accept)]
    public void Grant_grant_tokens_and_accept_are_protected(AnswerVariant variant) =>
        Assert.True(ApplyCommandChallenge.IsProtected(ApplyCommandKind.Answer, variant));

    [Theory]
    [InlineData(ApplyCommandKind.Answer, AnswerVariant.Text)]
    [InlineData(ApplyCommandKind.Answer, AnswerVariant.Resume)]
    [InlineData(ApplyCommandKind.Answer, AnswerVariant.Reject)]
    [InlineData(ApplyCommandKind.Command, null)]
    [InlineData(ApplyCommandKind.Cancel, null)]
    public void Text_resume_reject_commands_and_cancel_are_desk(ApplyCommandKind kind, AnswerVariant? variant) =>
        Assert.False(ApplyCommandChallenge.IsProtected(kind, variant));

    [Fact]
    public void A_protected_answer_names_its_amount_and_the_instance()
    {
        var address = InstanceAddress.From("T-1/step/1");

        Assert.Equal(
            ["grant 3 T-1/step/1", "grant-tokens 100000 T-1/step/1", "accept T-1/step/1"],
            [InstanceChallenge.Action(address, AnswerVariant.Grant, 3).Text, InstanceChallenge.Action(address, AnswerVariant.GrantTokens, 100_000).Text, InstanceChallenge.Action(address, AnswerVariant.Accept, amount: null).Text]);
    }

    [Fact]
    public void The_challenge_is_bound_to_the_address_and_the_instance_version()
    {
        var parked = View("T-1/step/1", "parked", version: 4, since: 1_000);
        var moved = parked with { State = "produce", Since = DateTimeOffset.FromUnixTimeMilliseconds(9_000) };   // same version: no event between them
        var later = parked with { Version = 5 };

        // why: the owner's decision (P4 integration): the subject is "address and version" (spec §4.2); a challenge
        // issued for v4 is stale once the instance has mirrored another event, whatever its state or since show
        Assert.Equal("instance:T-1/step/1@v4", InstanceChallenge.Subject(parked).Key);
        Assert.Equal(InstanceChallenge.Subject(parked), InstanceChallenge.Subject(moved));
        Assert.NotEqual(InstanceChallenge.Subject(parked), InstanceChallenge.Subject(later));
        Assert.NotEqual(InstanceChallenge.Subject(parked), InstanceChallenge.Subject(View("T-2/step/1", "parked", version: 4, since: 1_000)));
    }

    private static InstanceView View(string address, string state, int version, long since) => new(
        InstanceAddress.From(address),
        new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64))),
        state,
        Detail: null,
        DateTimeOffset.FromUnixTimeMilliseconds(since),
        string.Empty,
        [],
        Question: null,
        Task: null,
        Run: null,
        Children: [],
        Outcome: null)
    { Id = WorkflowInstanceId.Of(InstanceAddress.From(address)), Version = version };
}
