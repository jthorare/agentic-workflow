using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Testing.Challenges;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// An answer to a parent whose open park mirrors a child (engine spec rev 8 §5.8, W17; P7 D5): it goes to the child as the
/// child's own Update, under a nonce minted for the child, with 'onBehalfOf' naming the parent and the actor, and a
/// protected answer's challenge is bound to the child's address. Cancel, takeover and handback stay on the parent.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ApplyCommandForwardingTests
{
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('c', 32));
    private static readonly InstanceAddress _child = InstanceAddress.From("T-1/step/1/step.1");

    private readonly FakeTimeProvider _clock = new();
    private readonly TemporalLikeRuntime _instance = new();
    private readonly InMemoryChallenges _challenges;
    private readonly ApplyCommandService _service;

    public ApplyCommandForwardingTests()
    {
        _challenges = new InMemoryChallenges(_clock);
        _service = new ApplyCommandService(_instance, _instance, _instance, new InMemoryCommandNonces(_clock), _instance);
        _instance.MirrorChild(_child);
    }

    [Fact(Timeout = 30_000)]
    public async Task An_answer_to_a_mirroring_parent_goes_to_the_child_on_behalf_of_the_parent()
    {
        var request = new ApplyCommandRequest(_instance.Address.Value, ApplyCommandKind.Answer, AnswerVariant.Text, Text: "try the other file", Nonce: _nonce);

        var sent = await _service.Handle(request, TestContext.Current.CancellationToken);

        Assert.False(sent.IsError, sent.IsError ? sent.Error.Message : null);
        var (to, trigger) = Assert.Single(_instance.Delivered);
        var answer = Assert.IsType<AnswerGiven>(trigger);
        Assert.Equal(_child, to);
        Assert.Equal(new OnBehalfOf(WorkflowInstanceId.Of(_instance.Address), _instance.Current!.Actor), answer.Forwarded);
        Assert.Equal("try the other file", answer.Text);
        Assert.NotEqual(_nonce, answer.Stamp.Nonce);   // the child's own nonce, not the parent's
    }

    [Fact(Timeout = 30_000)]
    public async Task The_childs_nonce_is_the_same_on_a_retry_so_the_child_applies_it_once()
    {
        var request = new ApplyCommandRequest(_instance.Address.Value, ApplyCommandKind.Answer, AnswerVariant.Text, Text: "again", Nonce: _nonce);
        _instance.LoseNextResponse();

        _ = await _service.Handle(request, TestContext.Current.CancellationToken);
        _ = await _service.Handle(request, TestContext.Current.CancellationToken);

        Assert.Equal(2, _instance.Sent.Count);
        Assert.Equal(_instance.Sent[0], _instance.Sent[1]);
        Assert.NotEqual(_nonce, _instance.Sent[0]);
        Assert.Equal(1, _instance.Applied);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_protected_grant_with_no_challenge_is_forwarded_to_the_child()
    {
        var right = await _service.Handle(Grant(), TestContext.Current.CancellationToken);

        Assert.False(right.IsError, right.IsError ? right.Error.Message : null);
        Assert.Equal(_child, Assert.Single(_instance.Delivered).To);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_challenge_issued_for_a_mirroring_parent_names_the_child_and_redeems_on_the_forwarded_answer()
    {
        var issuer = new IssueChallengeService(null!, null!, _challenges, null!, _instance, _instance);   // why: a protected answer's issue path reads only the directory, the runtime and the challenges

        var issued = await issuer.Handle(new IssueChallengeRequest("grant 1 " + _instance.Address.Value), TestContext.Current.CancellationToken);
        Assert.False(issued.IsError, issued.IsError ? issued.Error.Message : null);
        var typed = issued.Value.Action;
        var sent = await _service.Handle(
            new ApplyCommandRequest(_instance.Address.Value, ApplyCommandKind.Answer, AnswerVariant.Grant, Amount: 1, Challenge: new ChallengeAnswer(issued.Value.Challenge, typed)),
            TestContext.Current.CancellationToken);

        Assert.Equal("grant 1 " + _child.Value, typed);
        Assert.False(sent.IsError, sent.IsError ? sent.Error.Message : null);
        Assert.Equal(_child, Assert.Single(_instance.Delivered).To);
    }

    [Theory(Timeout = 30_000)]
    [InlineData(ApplyCommandKind.Cancel)]
    [InlineData(ApplyCommandKind.Takeover)]
    [InlineData(ApplyCommandKind.Handback)]
    public async Task Cancel_and_takeover_act_on_the_parent(ApplyCommandKind kind)
    {
        var sent = await _service.Handle(new ApplyCommandRequest(_instance.Address.Value, kind, Nonce: _nonce), TestContext.Current.CancellationToken);

        Assert.False(sent.IsError, sent.IsError ? sent.Error.Message : null);
        Assert.Equal(_instance.Address, Assert.Single(_instance.Delivered).To);
        Assert.Equal(_nonce, Assert.Single(_instance.Sent));
    }

    private ApplyCommandRequest Grant() =>
        new(_instance.Address.Value, ApplyCommandKind.Answer, AnswerVariant.Grant, Amount: 1);
}
