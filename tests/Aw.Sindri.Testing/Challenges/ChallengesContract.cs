using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Challenges;

/// <summary>
/// The contract every <see cref="IChallenges"/> honours (engine spec §4.2): a challenge redeems once, within 5 minutes,
/// for its own action typed exactly and an unchanged subject; anything else is the caller's stale error. Run against
/// the fake and the in-process adapter (P2).
/// </summary>
public abstract class ChallengesContract
{
    private static RecipeHash Hash(string seed) => RecipeHash.From("sha256:" + seed.PadRight(64, '0'));

    private static readonly ChallengeAction _approve = ChallengeAction.Approve(RecipeId.From("workflow.step"), 1);
    private static readonly ChallengeSubject _subject = ChallengeSubject.ForRecipe(Hash("step"), 1, pinsDigest: null);

    /// <summary>The clock the challenges read; tests advance it.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>Fresh challenges on <see cref="Clock"/>.</summary>
    /// <returns>The challenges.</returns>
    protected abstract IChallenges Create();

    /// <summary>The action text is the one the human types: 'approve workflow.step 1'.</summary>
    [Fact]
    public void Approve_action_text_names_the_id_and_version() => Expect.Equal("approve workflow.step 1", _approve.Text);

    /// <summary>A challenge answered with its action typed exactly redeems once; the second redeem is stale.</summary>
    [Fact]
    public void Challenge_redeems_once()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);
        var answer = new ChallengeAnswer(issued.Id, "approve workflow.step 1");

        Expect.Equal(issued, challenges.Redeem(answer, _approve, _subject, WorkflowErrors.RecipeChallengeStale).Value);
        Expect.Equal(WorkflowErrors.RecipeChallengeStale.Code, challenges.Redeem(answer, _approve, _subject, WorkflowErrors.RecipeChallengeStale).Error.Code);
    }

    /// <summary>After 5 minutes the challenge is refused with the caller's stale error.</summary>
    [Fact]
    public void Expired_challenge_is_refused()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);
        Clock.Advance(IssuedChallenge.Lifetime);

        Expect.Equal(WorkflowErrors.RecipeChallengeStale.Code,
            challenges.Redeem(new ChallengeAnswer(issued.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale).Error.Code);
    }

    /// <summary>Typed text that differs, a request for another action, or a changed subject is refused.</summary>
    [Fact]
    public void Mistyped_other_action_or_changed_subject_is_refused()
    {
        var challenges = Create();
        var other = ChallengeAction.Approve(RecipeId.From("workflow.step"), 2);
        var moved = ChallengeSubject.ForRecipe(Hash("step"), 1, pinsDigest: "sha256:changed");

        Expect.True(challenges.Redeem(new ChallengeAnswer(challenges.Issue(_approve, _subject).Id, "approve workflow.step"), _approve, _subject, WorkflowErrors.RecipeChallengeStale).IsError);
        Expect.True(challenges.Redeem(new ChallengeAnswer(challenges.Issue(_approve, _subject).Id, _approve.Text), other, _subject, WorkflowErrors.RecipeChallengeStale).IsError);
        Expect.True(challenges.Redeem(new ChallengeAnswer(challenges.Issue(_approve, _subject).Id, _approve.Text), _approve, moved, WorkflowErrors.RecipeChallengeStale).IsError);
    }

    /// <summary>The typed text is compared after trimming the whitespace a terminal adds around it.</summary>
    [Fact]
    public void Typed_text_is_compared_after_trimming()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);

        Expect.Equal(issued, challenges.Redeem(new ChallengeAnswer(issued.Id, " approve workflow.step 1 "), _approve, _subject, WorkflowErrors.RecipeChallengeStale).Value);
    }

    /// <summary>Any attempt uses the challenge up: a refused one can't be retried with the right text.</summary>
    [Fact]
    public void A_refused_attempt_uses_the_challenge_up()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);
        _ = challenges.Redeem(new ChallengeAnswer(issued.Id, "approve workflow.step"), _approve, _subject, WorkflowErrors.RecipeChallengeStale);

        Expect.True(challenges.Redeem(new ChallengeAnswer(issued.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale).IsError);
    }

    /// <summary>A challenge still redeems one tick before it expires.</summary>
    [Fact]
    public void A_challenge_redeems_just_before_it_expires()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);
        Clock.Advance(IssuedChallenge.Lifetime - TimeSpan.FromTicks(1));

        Expect.True(challenges.Redeem(new ChallengeAnswer(issued.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale).IsSuccess);
    }

    private static readonly ChallengeSubject _stale = ChallengeSubject.ForRecipe(Hash("step"), 1, pinsDigest: "sha256:changed");
    private static readonly TimeSpan _slipGuard = IssuedChallenge.EarliestAnswer;

    private static IssuedChallenge IssueFor(IChallenges challenges, ChallengeMode mode, ChallengeChannel channel = ChallengeChannel.App) =>
        challenges.Issue(_approve, _subject, mode, channel);

    private static string Code(Kernel.Result<IssuedChallenge> redeemed) => redeemed.IsError ? redeemed.Error.Code : "ok";

    /// <summary>An app challenge records its mode, its channel and when it was issued; the older Issue is a typed terminal one.</summary>
    [Fact]
    public void An_issued_challenge_records_its_mode_channel_and_time()
    {
        var challenges = Create();
        var issued = IssueFor(challenges, ChallengeMode.Held);

        Expect.Equal((ChallengeMode.Held, ChallengeChannel.App, KnownInstants.Epoch), (issued.Mode, issued.Channel, issued.IssuedAt));
        Expect.Equal((ChallengeMode.Typed, ChallengeChannel.Terminal), (challenges.Issue(_approve, _subject).Mode, challenges.Issue(_approve, _subject).Channel));
    }

    /// <summary>A held challenge redeems by a hold of the minimum time once the slip guard has passed.</summary>
    [Fact]
    public void A_held_challenge_redeems_by_a_long_enough_hold()
    {
        var challenges = Create();
        var issued = IssueFor(challenges, ChallengeMode.Held);
        Clock.Advance(_slipGuard);

        Expect.Equal("ok", Code(challenges.Redeem(new ChallengeAnswer(issued.Id, HeldMs: IssuedChallenge.MinHoldMs), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
    }

    /// <summary>A hold under the minimum is SND-SETUP-003 and leaves the challenge open, so the owner can hold again.</summary>
    [Fact]
    public void A_short_hold_is_refused_without_using_the_challenge_up()
    {
        var challenges = Create();
        var issued = IssueFor(challenges, ChallengeMode.Held);
        Clock.Advance(_slipGuard);

        Expect.Equal("SND-SETUP-003", Code(challenges.Redeem(new ChallengeAnswer(issued.Id, HeldMs: IssuedChallenge.MinHoldMs - 1), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
        Expect.Equal("ok", Code(challenges.Redeem(new ChallengeAnswer(issued.Id, HeldMs: IssuedChallenge.MinHoldMs), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
    }

    /// <summary>An app answer under 2 seconds after issue, held or typed, is SND-SETUP-003 and leaves the challenge open.</summary>
    [Fact]
    public void An_app_answer_under_two_seconds_after_issue_is_refused_without_using_the_challenge_up()
    {
        var challenges = Create();
        var held = IssueFor(challenges, ChallengeMode.Held);
        var typed = challenges.Issue(ChallengeAction.Approve(RecipeId.From("workflow.step"), 2), _subject, ChallengeMode.Typed, ChallengeChannel.App);
        Clock.Advance(_slipGuard - TimeSpan.FromTicks(1));

        Expect.Equal("SND-SETUP-003", Code(challenges.Redeem(new ChallengeAnswer(held.Id, HeldMs: 5000), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
        Expect.Equal("SND-SETUP-003", Code(challenges.Redeem(new ChallengeAnswer(typed.Id, typed.Action.Text), typed.Action, _subject, WorkflowErrors.RecipeChallengeStale)));
        Clock.Advance(TimeSpan.FromTicks(1));
        Expect.Equal("ok", Code(challenges.Redeem(new ChallengeAnswer(typed.Id, typed.Action.Text), typed.Action, _subject, WorkflowErrors.RecipeChallengeStale)));
        Expect.Equal("ok", Code(challenges.Redeem(new ChallengeAnswer(held.Id, HeldMs: 5000), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
    }

    /// <summary>A typed challenge is never redeemed by a hold; the refusal leaves it open for the typed text.</summary>
    [Fact]
    public void A_typed_challenge_is_not_redeemed_by_a_hold()
    {
        var challenges = Create();
        var issued = IssueFor(challenges, ChallengeMode.Typed);
        Clock.Advance(_slipGuard);

        Expect.Equal("SND-SETUP-003", Code(challenges.Redeem(new ChallengeAnswer(issued.Id, HeldMs: 60_000), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
        Expect.Equal("ok", Code(challenges.Redeem(new ChallengeAnswer(issued.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
    }

    /// <summary>A terminal challenge is typed at the terminal: a hold is the wrong kind of answer.</summary>
    [Fact]
    public void A_terminal_challenge_is_not_redeemed_by_a_hold()
    {
        var challenges = Create();
        var issued = IssueFor(challenges, ChallengeMode.Typed, ChallengeChannel.Terminal);
        Clock.Advance(_slipGuard);

        Expect.Equal("SND-SETUP-003", Code(challenges.Redeem(new ChallengeAnswer(issued.Id, HeldMs: 60_000), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
    }

    /// <summary>A terminal challenge can be answered at once: the slip guard is the app's.</summary>
    [Fact]
    public void A_terminal_challenge_is_not_held_back_by_the_slip_guard()
    {
        var challenges = Create();
        var issued = IssueFor(challenges, ChallengeMode.Typed, ChallengeChannel.Terminal);

        Expect.Equal("ok", Code(challenges.Redeem(new ChallengeAnswer(issued.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
    }

    /// <summary>A held challenge also redeems by its typed alternative; a mistyped one is stale and uses it up, as at the terminal.</summary>
    [Fact]
    public void A_held_challenge_redeems_by_its_typed_alternative()
    {
        var challenges = Create();
        var ok = IssueFor(challenges, ChallengeMode.Held);
        Clock.Advance(_slipGuard);
        Expect.Equal("ok", Code(challenges.Redeem(new ChallengeAnswer(ok.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));

        var wrong = IssueFor(challenges, ChallengeMode.Held);
        Clock.Advance(_slipGuard);
        Expect.Equal(WorkflowErrors.RecipeChallengeStale.Code, Code(challenges.Redeem(new ChallengeAnswer(wrong.Id, "approve workflow.step"), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
        Expect.Equal(WorkflowErrors.RecipeChallengeStale.Code, Code(challenges.Redeem(new ChallengeAnswer(wrong.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
    }

    /// <summary>A hold against a subject that changed since the preview is stale and uses the challenge up.</summary>
    [Fact]
    public void A_hold_on_a_changed_subject_is_stale()
    {
        var challenges = Create();
        var issued = IssueFor(challenges, ChallengeMode.Held);
        Clock.Advance(_slipGuard);

        Expect.Equal(WorkflowErrors.RecipeChallengeStale.Code, Code(challenges.Redeem(new ChallengeAnswer(issued.Id, HeldMs: IssuedChallenge.MinHoldMs), _approve, _stale, WorkflowErrors.RecipeChallengeStale)));
        Expect.Equal(WorkflowErrors.RecipeChallengeStale.Code, Code(challenges.Redeem(new ChallengeAnswer(issued.Id, HeldMs: IssuedChallenge.MinHoldMs), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
    }

    /// <summary>A new challenge for the same action on the same channel replaces the open one; one on the other channel does not.</summary>
    [Fact]
    public void A_second_challenge_replaces_the_first_on_its_own_channel_only()
    {
        var challenges = Create();
        var firstApp = IssueFor(challenges, ChallengeMode.Typed);
        var terminal = IssueFor(challenges, ChallengeMode.Typed, ChallengeChannel.Terminal);
        var secondApp = IssueFor(challenges, ChallengeMode.Typed);
        Clock.Advance(_slipGuard);

        Expect.Equal(WorkflowErrors.RecipeChallengeStale.Code, Code(challenges.Redeem(new ChallengeAnswer(firstApp.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
        Expect.Equal("ok", Code(challenges.Redeem(new ChallengeAnswer(terminal.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
        Expect.Equal("ok", Code(challenges.Redeem(new ChallengeAnswer(secondApp.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale)));
    }
}
