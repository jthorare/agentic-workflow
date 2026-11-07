using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>
/// The issue sheet's comment box (Linear hub spec §3, "Issue sheet, Post comment"; D7, D12): its own Post and client id, "Sending…" after 300 ms, "Checking ENG-12…" after 15 s,
/// posted, slow, unknown with Retry on the same client id, the refusals with the text kept, and typed text counting as unsaved without touching the form's Save.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LinearIssueCommentTests
{
    private const string Note = "Budget resets at midnight UTC: agreed.";

    private readonly LinearIssueEditFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_is_off_with_nothing_typed_or_only_spaces_and_on_once_there_is_text()
    {
        var sheet = await _f.Opened(Token);
        var empty = (sheet.CanComment, sheet.CanPost, sheet.PostText);

        sheet.CommentText = "  \n ";
        var spaces = sheet.CanPost;
        sheet.CommentText = Note;

        Assert.Equal((true, false, "Post"), empty);
        Assert.False(spaces);
        Assert.True(sheet.CanPost);
    }

    [Fact]
    public async Task Post_sends_the_text_as_typed_with_a_client_id_made_at_the_first_character()
    {
        var sheet = await _f.Opened(Token);
        sheet.CommentText = "B";
        sheet.CommentText = Note;

        await sheet.Post(Token);

        var sent = Assert.Single(_f.Server.LinearComments);
        Assert.Equal(("ENG-12", Note), (sent.Key, sent.Body));
        Assert.False(string.IsNullOrWhiteSpace(sent.ClientId));
    }

    [Fact]
    public async Task Posted_clears_the_box_says_posted_puts_the_comment_first_and_keeps_focus_in_the_box()
    {
        var sheet = await _f.Opened(Token);
        var before = sheet.Comments.Count;
        sheet.CommentText = Note;

        await sheet.Post(Token);

        Assert.Equal((string.Empty, "Posted", false), (sheet.CommentText, sheet.CommentStatus, sheet.CanPost));
        Assert.Equal(before + 1, sheet.Comments.Count);
        Assert.Equal(("Joi Example", Note), (sheet.Comments[0].Author, string.Concat(sheet.Comments[0].Lines.Select(l => l.Text))));
        Assert.Equal([LinearIssueFocus.CommentBox], _f.Focus);
    }

    [Fact]
    public async Task A_slow_answer_says_linear_was_slow_to_answer()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearComment(RouteAnswer.Api("linear-comment", "applied-slow"));
        sheet.CommentText = Note;

        await sheet.Post(Token);

        Assert.Equal(("Posted (Linear was slow to answer)", string.Empty), (sheet.CommentStatus, sheet.CommentText));
    }

    [Fact]
    public async Task While_posting_the_box_locks_at_once_and_post_says_sending_only_after_300_ms()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.LinearCommentGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sheet.CommentText = Note;

        var posting = sheet.Post(Token);
        var at0 = (sheet.IsPosting, sheet.CanComment, sheet.CanPost, sheet.PostText);
        _f.Clock.Advance(TimeSpan.FromMilliseconds(300));
        var at300 = sheet.PostText;
        _f.Server.LinearCommentGate.SetResult();
        await posting;

        Assert.Equal((true, false, false, "Post"), at0);
        Assert.Equal("Sending…", at300);
        Assert.Equal((false, true, "Post"), (sheet.IsPosting, sheet.CanComment, sheet.PostText));
    }

    [Fact]
    public async Task After_15_s_without_an_answer_the_line_says_it_is_checking_the_issue()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.LinearCommentGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sheet.CommentText = Note;

        var posting = sheet.Post(Token);
        _f.Clock.Advance(TimeSpan.FromSeconds(15));
        var checking = sheet.CommentStatus;
        _f.Server.LinearCommentGate.SetResult();
        await posting;

        Assert.Equal("Linear didn't confirm it. Checking ENG-12…", checking);
        Assert.Equal("Posted", sheet.CommentStatus);
    }

    [Fact]
    public async Task Unknown_keeps_the_box_locked_asks_retry_and_retry_sends_the_same_client_id_and_text()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearComment(RouteAnswer.Problem("linear-comment", "unknown"));
        sheet.CommentText = Note;

        await sheet.Post(Token);
        var unknown = (sheet.CommentStatus, sheet.CanRetryComment, sheet.CanComment, sheet.CanPost, sheet.CommentText);
        await sheet.RetryComment(Token);

        Assert.Equal(("Linear didn't confirm the comment. Retry?", true, false, false, Note), unknown);
        Assert.Equal(2, _f.Server.LinearComments.Count);
        Assert.Equal((_f.Server.LinearComments[0].ClientId, Note), (_f.Server.LinearComments[1].ClientId, _f.Server.LinearComments[1].Body));
        Assert.Equal(("Posted", false, string.Empty), (sheet.CommentStatus, sheet.CanRetryComment, sheet.CommentText));
    }

    // A comment is a create: once the app's own call has no answer (its 120 s deadline, or a reset mid-answer), the POST may have
    // reached Sindri, so the box can't be handed back for editing. SND-UI-002 doesn't say whether anything was sent, so every one is unknown.
    [Fact]
    public async Task No_answer_from_Sindri_is_unknown_the_box_stays_locked_and_retry_sends_the_same_client_id_and_text()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearComment(RouteAnswer.Problem(HttpStatusCode.GatewayTimeout, "SND-UI-002", "127.0.0.1:7410 didn't answer within 120 s"));
        sheet.CommentText = Note;

        await sheet.Post(Token);
        var unknown = (sheet.CommentStatus, sheet.CommentError, sheet.CanRetryComment, sheet.CanComment, sheet.CommentText);
        await sheet.RetryComment(Token);

        Assert.Equal(("Linear didn't confirm the comment. Retry?", string.Empty, true, false, Note), unknown);
        Assert.Equal(2, _f.Server.LinearComments.Count);
        Assert.Equal((_f.Server.LinearComments[0].ClientId, Note), (_f.Server.LinearComments[1].ClientId, _f.Server.LinearComments[1].Body));
        Assert.Equal("Posted", sheet.CommentStatus);
    }

    [Fact]
    public async Task Posted_goes_at_the_next_comments_first_character()
    {
        var sheet = await _f.Opened(Token);
        sheet.CommentText = Note;
        await sheet.Post(Token);

        sheet.CommentText = "A";

        Assert.Equal(string.Empty, sheet.CommentStatus);
    }

    [Fact]
    public async Task The_next_comment_after_one_is_posted_gets_a_new_client_id()
    {
        var sheet = await _f.Opened(Token);
        sheet.CommentText = Note;
        await sheet.Post(Token);

        sheet.CommentText = "And one more.";
        await sheet.Post(Token);

        Assert.False(string.Equals(_f.Server.LinearComments[0].ClientId, _f.Server.LinearComments[1].ClientId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_rate_limit_keeps_the_text_and_says_when_to_try_again()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearComment(RouteAnswer.Problem("linear-change", "rate-limited"));
        sheet.CommentText = Note;

        await sheet.Post(Token);

        Assert.Equal(($"Linear's rate limit: try again after {LinearIssueEditFixture.Local("2026-01-01T10:06:00Z")}", Note, true), (sheet.CommentError, sheet.CommentText, sheet.CanPost));
    }

    [Fact]
    public async Task Linear_unreachable_keeps_the_text_and_says_nothing_was_changed()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearComment(RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-LINEAR-010", "Linear unreachable"));
        sheet.CommentText = Note;

        await sheet.Post(Token);

        Assert.Equal(("Couldn't reach Linear, so nothing was changed. Try again.", Note, true), (sheet.CommentError, sheet.CommentText, sheet.CanPost));
    }

    [Fact]
    public async Task Typing_again_after_a_refusal_clears_the_banner()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearComment(RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-LINEAR-010", "Linear unreachable"));
        sheet.CommentText = Note;
        await sheet.Post(Token);

        sheet.CommentText = Note + " Again.";

        Assert.Equal(string.Empty, sheet.CommentError);
    }

    [Fact]
    public async Task A_session_refusal_keeps_the_text_and_turns_the_box_off()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearComment(RouteAnswer.Problem("linear-comment", "session-refused"));
        sheet.CommentText = Note;

        await sheet.Post(Token);

        Assert.Equal((false, false, false, Note), (sheet.CanEdit, sheet.CanComment, sheet.CanPost, sheet.CommentText));
    }

    [Fact]
    public async Task Without_write_access_the_box_is_off()
    {
        _f.Serve(LinearIssueEditFixture.Issue("no-write-key"));
        var sheet = await _f.Opened(Token);

        sheet.CommentText = Note;

        Assert.Equal((false, false), (sheet.CanComment, sheet.CanPost));
    }

    [Fact]
    public async Task Over_65536_characters_post_is_off_and_a_note_says_the_cap()
    {
        var sheet = await _f.Opened(Token);

        sheet.CommentText = new string('a', 65_537);
        var over = (sheet.CanPost, sheet.CommentNote);
        sheet.CommentText = new string('a', 65_536);

        Assert.Equal((false, "Comments can be up to 65,536 characters."), over);
        Assert.Equal((true, string.Empty), (sheet.CanPost, sheet.CommentNote));
    }

    [Fact]
    public async Task A_typed_comment_does_not_turn_on_save_and_a_refresh_still_redraws_the_fields()
    {
        var sheet = await _f.Opened(Token);
        sheet.CommentText = Note;
        var issue = LinearIssueEditFixture.Issue("writable");
        issue["title"] = "Renamed in Linear";
        _f.Serve(issue);

        await sheet.Refresh(LinearIssueEditFixture.Url, Token);

        Assert.Equal((false, false, "Renamed in Linear", Note), (sheet.HasChanges, sheet.CanSave, sheet.TitleField.Text, sheet.CommentText));
    }

    [Fact]
    public async Task A_not_synced_refresh_keeps_a_typed_comment()
    {
        var sheet = await _f.Opened(Token);
        sheet.CommentText = Note;
        _f.Server.LinearIssueAnswer = RouteAnswer.Problem("linear-issue", "not-found");

        await sheet.Refresh(LinearIssueEditFixture.Url, Token);

        Assert.Equal((true, Note, true), (sheet.IsShown, sheet.CommentText, sheet.CanPost));
    }

    [Fact]
    public async Task Leaving_with_only_a_typed_comment_asks_and_says_the_comment_is_not_posted()
    {
        var sheet = await _f.Opened(Token);
        sheet.CommentText = Note;

        var asked = sheet.Leaving();

        Assert.Equal((true, true, true, "Your comment isn't posted yet."), (asked, sheet.HasUnsaved, sheet.IsConfirmingDiscard, sheet.DiscardBody));
    }

    [Fact]
    public async Task Leaving_with_a_field_edit_and_a_comment_names_both()
    {
        var sheet = await _f.Opened(Token);
        _f.Pick("Priority", "High");
        sheet.CommentText = Note;

        _ = sheet.Leaving();

        Assert.Equal("Your edits to Priority aren't saved yet. Your comment isn't posted yet.", sheet.DiscardBody);
    }

    [Fact]
    public async Task Discard_drops_the_typed_comment_and_lets_the_sheet_go()
    {
        var sheet = await _f.Opened(Token);
        var left = 0;
        sheet.LeaveConfirmed += (_, _) => left++;
        sheet.CommentText = Note;
        _ = sheet.Leaving();

        sheet.Discard();

        Assert.Equal((string.Empty, false, 1), (sheet.CommentText, sheet.HasUnsaved, left));
    }

    [Fact]
    public async Task Discard_waits_while_a_comment_is_out()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.LinearCommentGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sheet.CommentText = Note;
        var posting = sheet.Post(Token);
        _ = sheet.Leaving();

        sheet.Discard();
        var held = (sheet.IsConfirmingDiscard, sheet.CommentText);
        _f.Server.LinearCommentGate.SetResult();
        await posting;

        Assert.Equal((true, Note), held);
    }

    [Fact]
    public async Task A_posted_comment_settles_the_question_when_nothing_else_is_unsaved()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.LinearCommentGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sheet.CommentText = Note;
        var posting = sheet.Post(Token);
        _ = sheet.Leaving();

        _f.Server.LinearCommentGate.SetResult();
        await posting;

        Assert.Equal((false, false), (sheet.IsConfirmingDiscard, sheet.HasUnsaved));
    }

    [Fact]
    public async Task An_answer_after_the_sheet_moved_to_another_issue_is_dropped()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.LinearCommentGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sheet.CommentText = Note;
        var posting = sheet.Post(Token);

        await sheet.Open(LinearIssueEditFixture.Url, "ENG-13", Token);
        _f.Server.LinearCommentGate.SetResult();
        await posting;

        Assert.Equal(("ENG-13", string.Empty, string.Empty, true), (sheet.Key, sheet.CommentText, sheet.CommentStatus, sheet.CanComment));
    }
}
