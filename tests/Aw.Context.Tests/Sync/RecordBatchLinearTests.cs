using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using static Aw.Context.Testing.Records.LinearSideSamples;

namespace Aw.Context.Tests.Sync;

/// <summary>What <see cref="RecordBatches"/> does with the side rows a Linear issue item carries (Linear hub spec 2026-11-04 §4.1, D4): the comments are cleaned like the text, and the fields pass through.</summary>
[Trait("Lane", "unit")]
public sealed class RecordBatchLinearTests
{
    private readonly Scrubbers _scrubbers = new FakeScrubberFactory().For([]).Value;

    private RecordWrite Build(LinearSideRows? side, string text = "Body")
    {
        var item = new RecordItem(
            RecordKey.From(Key), RecordKind.Issue, "Title", text, Author: null, Url: null, State: null, StateRaw: null,
            CreatedAt: null, UpdatedAt: null, ClosedAt: null, SourceVersion.From("v1"), [], Repo: null, Audience: null, Side: side);
        var page = new RecordPage([item], [], SourceCursor.From("c"), CursorReset: false);
        return RecordBatches.From(RecordSamples.Linear, page, SyncPhase.Incremental, _scrubbers, RecordSamples.Now).Records.Single();
    }

    [Fact]
    public void An_item_with_side_rows_hands_them_to_the_store_with_its_fields_untouched()
    {
        var rows = Rows(Fields(Ada, Todo), LinearWrittenFields.State, Comment("c1", "hello"));

        var write = Build(rows);

        var side = Assert.IsType<LinearSideRows>(write.Side);
        Assert.Equal(rows.Issue.Fields, side.Issue.Fields);
        Assert.Equal(LinearWrittenFields.State, side.Written);
        Assert.Equal("hello", Assert.Single(side.Issue.Comments).Body);
    }

    [Fact]
    public void An_item_without_side_rows_sends_none() =>
        Assert.Null(Build(side: null).Side);

    [Fact]
    public void A_comment_over_the_cap_is_cut()
    {
        var write = Build(Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("c1", new string('x', RecordBatches.MaxCommentBody + 500))));

        var body = Assert.IsType<LinearSideRows>(write.Side).Issue.Comments.Single().Body;

        Assert.Equal(RecordBatches.MaxCommentBody, body.Length);
        Assert.EndsWith("…", body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_comment_keeps_its_ids_author_time_and_marker()
    {
        var comment = Comment("c1", "hi", minutes: 3, Bo, sindri: true);

        var kept = Assert.IsType<LinearSideRows>(Build(Rows(Fields(Ada, Todo), LinearWrittenFields.None, comment)).Side).Issue.Comments.Single();

        Assert.Equal(comment, kept);
    }

    [Fact]
    public void A_secret_in_a_comment_is_redacted_before_it_is_stored()
    {
        var write = Build(Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("c1", $"token {FakeScrubber.Secret} here")));

        var body = Assert.IsType<LinearSideRows>(write.Side).Issue.Comments.Single().Body;

        Assert.Equal("token [REDACTED:fake] here", body);
    }

    [Fact]
    public void A_comment_the_scrubber_withholds_keeps_a_marker_and_its_neighbours_stay()
    {
        var write = Build(Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("c2", FakeScrubber.BlockMe), Comment("c1", "fine")));

        var comments = Assert.IsType<LinearSideRows>(write.Side).Issue.Comments;

        Assert.Equal([RecordBatches.WithheldComment, "fine"], comments.Select(static c => c.Body), StringComparer.Ordinal);
        Assert.Equal(["c2", "c1"], comments.Select(static c => c.Id.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_blocked_issue_keeps_its_fields_and_no_comments()
    {
        var write = Build(Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("c1", "fine")), text: FakeScrubber.BlockMe);

        var side = Assert.IsType<LinearSideRows>(write.Side);

        Assert.True(write.Record.Blocked);
        Assert.Equal("ENG-1", side.Issue.Fields.Identifier);
        Assert.Empty(side.Issue.Comments);
    }

    [Fact]
    public void The_overload_with_no_phase_builds_the_same_writes_with_no_position_and_no_guard()
    {
        var item = new RecordItem(
            RecordKey.From(Key), RecordKind.Issue, "Title", "Body", Author: null, Url: null, State: null, StateRaw: null,
            CreatedAt: null, UpdatedAt: null, ClosedAt: null, SourceVersion.From("v1"), [], Repo: null, Audience: null, Side: Rows(Fields(Ada, Todo)));
        var page = new RecordPage([item], [], SourceCursor.From("c"), CursorReset: false);

        var paged = RecordBatches.From(RecordSamples.Linear, page, SyncPhase.Incremental, _scrubbers, RecordSamples.Now);
        var single = RecordBatches.From(RecordSamples.Linear, page, _scrubbers, RecordSamples.Now);

        Assert.Null(single.Position);
        Assert.False(single.Guard);
        Assert.NotNull(paged.Position);
        Assert.Equal(paged.Records.Single().Record, single.Records.Single().Record);
        Assert.Equal(paged.Records.Single().Chunks, single.Records.Single().Chunks);
        Assert.Equal(paged.Records.Single().Side, single.Records.Single().Side);
    }

    [Fact]
    public void A_description_is_cleaned_like_the_text()
    {
        var write = Build(Described(Rows(Fields(Ada, Todo)), $"key {FakeScrubber.Secret} here"));

        Assert.Equal("key [REDACTED:fake] here", Assert.IsType<LinearSideRows>(write.Side).Issue.Description);
    }

    [Fact]
    public void A_description_the_scrubber_blocks_is_the_withheld_marker()
    {
        var write = Build(Described(Rows(Fields(Ada, Todo)), FakeScrubber.BlockMe));

        Assert.Equal(RecordBatches.WithheldComment, Assert.IsType<LinearSideRows>(write.Side).Issue.Description);
    }

    [Fact]
    public void A_description_over_64_KiB_is_cut_to_it()
    {
        var write = Build(Described(Rows(Fields(Ada, Todo)), new string('x', RecordBatches.MaxIssueDescription + 10)));

        var description = Assert.IsType<LinearSideRows>(write.Side).Issue.Description;

        Assert.Equal(65536, description?.Length);
        Assert.EndsWith("…", description, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_or_missing_description_is_null()
    {
        Assert.Null(Assert.IsType<LinearSideRows>(Build(Described(Rows(Fields(Ada, Todo)), "")).Side).Issue.Description);
        Assert.Null(Assert.IsType<LinearSideRows>(Build(Described(Rows(Fields(Ada, Todo)), description: null)).Side).Issue.Description);
    }
}
