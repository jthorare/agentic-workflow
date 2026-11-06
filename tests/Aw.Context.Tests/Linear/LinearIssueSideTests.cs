using System.Globalization;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Linear;

namespace Aw.Context.Tests.Linear;

/// <summary>What a Linear issue brings besides its record: its fields and its newest comments, and the text that leaves Sindri's own comment out (linear hub spec 2026-11-04 §4.1).</summary>
[Trait("Lane", "unit")]
public sealed class LinearIssueSideTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly ExternalId _sindri = ExternalId.From("user-sindri");

    private static LinearIssue Full() => new(
        "issue-1", "ENG-12", "Fix the login timeout", _at, "Body", Team: new LinearTeam("team-1", "ENG"), State: new LinearState("In Progress", "started", "state-3"),
        Assignee: new LinearNamed("user-ada", "Ada"), Priority: 2, Estimate: 5, Cycle: new LinearNamed("cycle-9", "Cycle 9"), Project: new LinearNamed("project-1", "Launch"),
        Parent: new LinearIssueRef("ENG-3", "Epic", "issue-0"), Labels: new LinearConnection<LinearNamed>([new LinearNamed("label-a"), new LinearNamed("label-b")]),
        DueDate: "2026-11-10", SortOrder: -1500.5);

    private static LinearIssue Bare() => new("issue-2", "ENG-13", "Nothing set", _at);

    private static LinearComment Comment(string id, string body, int minute, string? author = "user-ada") =>
        new(id, body, _at.AddMinutes(minute), author is null ? null : new LinearNamed(author, "Name of " + author));

    private static LinearIssue WithComments(params LinearComment[] comments) =>
        Bare() with { Comments = new LinearConnection<LinearComment>(comments) };

    [Fact]
    public void An_issue_with_everything_set_maps_every_field()
    {
        var fields = LinearIssueItem.Side(Full()).Fields;

        Assert.Equal(new LinearIssueFields(
            ExternalId.From("team-1"), "ENG-12", ExternalId.From("state-3"), "started", ExternalId.From("user-ada"), 2, 5, ExternalId.From("cycle-9"), ExternalId.From("project-1"),
            ExternalId.From("issue-0"), fields.LabelIds, "2026-11-10", -1500.5, _at), fields);
        Assert.Equal(["label-a", "label-b"], fields.LabelIds.Select(static l => l.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void An_issue_with_nothing_set_maps_nulls_and_priority_none()
    {
        var fields = LinearIssueItem.Side(Bare()).Fields;

        Assert.Equal("ENG-13", fields.Identifier);
        Assert.Null(fields.TeamId);
        Assert.Null(fields.StateId);
        Assert.Null(fields.StateType);
        Assert.Null(fields.AssigneeId);
        Assert.Equal(0, fields.Priority);
        Assert.Null(fields.Estimate);
        Assert.Null(fields.CycleId);
        Assert.Null(fields.ProjectId);
        Assert.Null(fields.ParentId);
        Assert.Empty(fields.LabelIds);
        Assert.Null(fields.DueDate);
        Assert.Null(fields.SortOrder);
        Assert.Equal(_at, fields.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Priority_zero_to_four_is_kept(int priority) => Assert.Equal(priority, LinearIssueItem.Side(Bare() with { Priority = priority }).Fields.Priority);

    [Fact]
    public void An_issue_with_no_comments_has_no_comment_rows() => Assert.Empty(LinearIssueItem.Side(Bare()).Comments);

    [Fact]
    public void A_comment_keeps_its_id_author_time_and_body()
    {
        var side = LinearIssueItem.Side(WithComments(Comment("c1", "first", minute: 3)));

        var row = Assert.Single(side.Comments);
        Assert.Equal(new LinearCommentRow(ExternalId.From("c1"), ExternalId.From("user-ada"), _at.AddMinutes(3), "first", Sindri: false), row);
    }

    [Fact]
    public void A_comment_with_no_user_has_no_author()
    {
        var row = Assert.Single(LinearIssueItem.Side(WithComments(Comment("c1", "from an integration", 1, author: null))).Comments);

        Assert.Null(row.AuthorId);
    }

    [Fact]
    public void Only_the_newest_fifty_comments_are_kept_newest_first()
    {
        var comments = Enumerable.Range(1, 60).Select(n => Comment("c" + n.ToString(CultureInfo.InvariantCulture), "body " + n.ToString(CultureInfo.InvariantCulture), n)).ToArray();

        var rows = LinearIssueItem.Side(WithComments(comments)).Comments;

        Assert.Equal(LinearIssueSide.MaxComments, rows.Count);
        Assert.Equal("c60", rows[0].Id.Value);
        Assert.Equal("c11", rows[^1].Id.Value);
    }

    [Fact]
    public void A_comment_is_sindris_when_it_carries_the_marker_and_the_write_user_wrote_it()
    {
        var issue = WithComments(Comment("c1", "Sindri run\n" + LinearMarker.Line("ENG-13", 1), 1, author: "user-sindri"));

        Assert.True(Assert.Single(LinearIssueItem.Side(issue, _sindri).Comments).Sindri);
    }

    [Fact]
    public void A_teammate_pasting_the_marker_is_not_flagged()
    {
        var issue = WithComments(Comment("c1", "quoting:\n" + LinearMarker.Line("ENG-13", 1), 1, author: "user-ada"));

        Assert.False(Assert.Single(LinearIssueItem.Side(issue, _sindri).Comments).Sindri);
    }

    [Fact]
    public void The_write_user_without_the_marker_is_not_flagged()
    {
        var issue = WithComments(Comment("c1", "I commented from the web app", 1, author: "user-sindri"));

        Assert.False(Assert.Single(LinearIssueItem.Side(issue, _sindri).Comments).Sindri);
    }

    [Fact]
    public void With_no_write_user_nothing_is_flagged()
    {
        var issue = WithComments(Comment("c1", LinearMarker.Line("ENG-13", 1), 1, author: "user-sindri"));

        Assert.False(Assert.Single(LinearIssueItem.Side(issue).Comments).Sindri);
    }

    [Fact]
    public void The_text_leaves_out_sindris_comment_and_keeps_the_others()
    {
        var issue = WithComments(
            Comment("c1", "Agreed, ship it", 1),
            Comment("c2", "Sindri run (automatic): scoping\n" + LinearMarker.Line("ENG-13", 1), 2, author: "user-sindri"),
            Comment("c3", "pasted:\n" + LinearMarker.Line("ENG-13", 1), 3));

        var text = LinearText.Of(issue, _sindri);

        Assert.Contains("Agreed, ship it", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Sindri run (automatic)", text, StringComparison.Ordinal);
        Assert.Contains("pasted:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_is_the_same_with_and_without_sindris_comment()
    {
        var plain = WithComments(Comment("c1", "Agreed, ship it", 1));
        var withSindri = WithComments(Comment("c1", "Agreed, ship it", 1), Comment("c2", "status\n" + LinearMarker.Line("ENG-13", 1), 2, author: "user-sindri"));

        Assert.Equal(LinearText.Of(plain, _sindri), LinearText.Of(withSindri, _sindri));
    }

    [Fact]
    public void The_record_text_leaves_out_sindris_comment()
    {
        var item = LinearIssueItem.Of(WithComments(Comment("c2", "status\n" + LinearMarker.Line("ENG-13", 1), 2, author: "user-sindri")), [], _sindri);

        Assert.DoesNotContain("status", item.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_keeps_the_newest_twenty_comments_though_the_feed_brings_fifty()
    {
        var comments = Enumerable.Range(1, 30).Select(n => Comment("c" + n.ToString(CultureInfo.InvariantCulture), "line-" + n.ToString(CultureInfo.InvariantCulture) + "-end", n)).Reverse().ToArray();

        var text = LinearText.Of(WithComments(comments));

        Assert.Equal(20, text.Split('\n').Count(static l => l.StartsWith("- Name of user-ada", StringComparison.Ordinal)));
        Assert.EndsWith("(more comments not shown)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_feed_version_ends_in_f3_so_every_stored_issue_is_saved_again_once()
    {
        var item = LinearIssueItem.Of(Bare(), []);

        Assert.Equal("2026-11-01T09:00:00.0000000Z;f3", item.Version.Value);
    }
}
