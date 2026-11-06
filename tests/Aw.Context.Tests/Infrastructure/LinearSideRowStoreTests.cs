using System.Globalization;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Records;
using Microsoft.Extensions.Time.Testing;
using static Aw.Context.Testing.Records.LinearSideSamples;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>What the record store does with an issue's Linear side rows: <c language="text">linear_issue</c>, <c language="text">linear_comment</c> and <c language="text">linear_issue_change</c> (Linear hub spec 2026-11-04 §4.1, D4).</summary>
public sealed class LinearSideRowStoreTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();
    private readonly FakeTimeProvider _clock = new(RecordSamples.Now);
    private readonly SqliteRecordStore _store;

    public LinearSideRowStoreTests()
    {
        _store = new SqliteRecordStore(_world.Database, [new LinearRowWriter(_clock)]);
    }

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<CommitReport> Commit(int page, params RecordWrite[] writes) =>
        (await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, page, writes), Ct)).Value;

    private async Task<CommitReport> Remove(int page, string key = Key) =>
        (await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, page) with { Removed = [RecordKey.From(key)] }, Ct)).Value;

    // A page of a feed that has finished a pass: the STORED cursor carries the watermark ('s='), which is what tells the writer a new issue is news and not the first sync. The writer reads the cursor
    // stored before this page's own, so an empty page with the watermark is committed first, the way a finished pass leaves it.
    private async Task<CommitReport> CommitAfterFirstPass(int page, params RecordWrite[] writes)
    {
        var position = new SyncPosition(SourceCursor.From("s=2026-11-06T00:00:00.0000000Z;a=;t="), SyncPhase.Incremental, BackfillCursor: null);
        _ = (await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, page) with { Position = position }, Ct)).Value;
        return (await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, page, writes) with { Position = position }, Ct)).Value;
    }

    // A page in the middle of the first pass: no watermark yet.
    private async Task<CommitReport> CommitDuringFirstPass(int page, params RecordWrite[] writes) =>
        (await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, page, writes) with
        {
            Position = new SyncPosition(SourceCursor.From($"s=;a=end-{page.ToString(CultureInfo.InvariantCulture)};t=2026-11-06T00:00:00.0000000Z"), SyncPhase.Recent, BackfillCursor: null),
        }, Ct)).Value;

    [Fact, Trait("Lane", "integration")]
    public async Task A_new_issue_after_the_first_pass_logs_the_assignee_as_a_change_from_nobody()
    {
        _ = await CommitAfterFirstPass(1, Write(Rows(Fields(Ada, Todo))));

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue_change WHERE issue_key = '{Key}' AND field = 'assignee' AND \"old\" IS NULL AND \"new\" = 'user-ada' AND via = 'feed'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change"));   // its first state is not a move
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_new_unassigned_issue_after_the_first_pass_logs_nothing()
    {
        _ = await CommitAfterFirstPass(1, Write(Rows(Fields(assignee: null, Todo))));

        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_new_issue_during_the_first_pass_logs_nothing()
    {
        _ = await CommitDuringFirstPass(1, Write(Rows(Fields(Ada, Todo))));

        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_f3_re_save_of_an_existing_record_logs_nothing_even_after_the_first_pass()
    {
        _ = await CommitAfterFirstPass(1, Write(side: null));   // stored by the feed version before side rows

        _ = await CommitAfterFirstPass(2, Write(Rows(Fields(Ada, Todo))));

        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_reassignment_after_the_first_pass_logs_one_row_per_hop()
    {
        _ = await CommitAfterFirstPass(1, Write(Rows(Fields(Bo, Todo))));
        _ = await CommitAfterFirstPass(2, Write(Rows(Fields(Ada, Todo, RecordSamples.Now.AddMinutes(1))), version: "v2"));

        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change WHERE field = 'assignee' AND \"old\" IS NULL AND \"new\" = 'user-bo'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change WHERE field = 'assignee' AND \"old\" = 'user-bo' AND \"new\" = 'user-ada'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_new_issue_stores_its_fields_and_comments_and_logs_no_change()
    {
        var rows = Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("c2", "second", minutes: 5), Comment("c1", "first", sindri: true));

        _ = await Commit(1, Write(rows));

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{Key}' AND identifier = 'ENG-1' AND team_id = 'team-1' AND state_id = 'state-todo' AND state_type = 'unstarted' AND assignee_id = 'user-ada' AND priority = 2 AND estimate = 3 AND cycle_id = 'cycle-1' AND project_id = 'project-1' AND parent_id IS NULL AND labels = '[\"label-a\",\"label-b\"]' AND due_date = '2026-11-20' AND sort_order = -10.5"));
        Assert.Equal(2, _world.Count($"SELECT count(*) FROM linear_comment WHERE issue_key = '{Key}'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_comment WHERE id = 'c1' AND sindri = 1 AND body = 'first' AND author_id = 'user-ada'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_changed_assignee_and_state_log_one_row_each_from_the_feed()
    {
        _ = await Commit(1, Write(Rows(Fields(Ada, Todo))));
        _clock.Advance(TimeSpan.FromHours(1));

        _ = await Commit(2, Write(Rows(Fields(Bo, Doing, RecordSamples.Now.AddHours(1))), version: "v2"));

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue_change WHERE issue_key = '{Key}' AND field = 'assignee' AND \"old\" = 'user-ada' AND \"new\" = 'user-bo' AND via = 'feed'"));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue_change WHERE issue_key = '{Key}' AND field = 'state' AND \"old\" = 'state-todo' AND \"new\" = 'state-doing' AND via = 'feed'"));
        Assert.Equal(2, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unassigned_issue_logs_a_null_old_and_a_dropped_assignee_a_null_new()
    {
        _ = await Commit(1, Write(Rows(Fields(assignee: null, Todo))));
        _ = await Commit(2, Write(Rows(Fields(Ada, Todo, RecordSamples.Now.AddMinutes(1))), version: "v2"));
        _ = await Commit(3, Write(Rows(Fields(assignee: null, Todo, RecordSamples.Now.AddMinutes(2))), version: "v3"));

        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change WHERE field = 'assignee' AND \"old\" IS NULL AND \"new\" = 'user-ada'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change WHERE field = 'assignee' AND \"old\" = 'user-ada' AND \"new\" IS NULL"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Only_the_field_a_write_sent_is_logged_as_write_through()
    {
        _ = await Commit(1, Write(Rows(Fields(Ada, Todo))));

        // the owner saved a new state; a teammate's reassignment was absorbed by the same re-read
        _ = await Commit(2, Write(Rows(Fields(Bo, Doing, RecordSamples.Now.AddMinutes(1)), LinearWrittenFields.State), version: "v2"));

        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change WHERE field = 'state' AND via = 'write-through'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change WHERE field = 'assignee' AND via = 'feed'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_change_in_another_field_logs_nothing()
    {
        _ = await Commit(1, Write(Rows(Fields(Ada, Todo, priority: 2))));

        _ = await Commit(2, Write(Rows(Fields(Ada, Todo, RecordSamples.Now.AddMinutes(1), priority: 4)), version: "v2"));

        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue WHERE priority = 4"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Rows_that_differ_are_rewritten_when_the_record_is_unchanged_and_that_logs_the_change_but_a_first_sight_does_not()
    {
        _ = await Commit(1, Write(side: null));   // a record stored before this feed version carried side rows

        var first = await Commit(2, Write(Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("c1", "hello"))));
        var again = await Commit(3, Write(Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("c1", "hello"))));
        var edited = await Commit(4, Write(Rows(Fields(Bo, Todo), LinearWrittenFields.None, Comment("c1", "hello"))));

        Assert.Equal(1, first.Unchanged);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue"));
        Assert.Equal(1, again.Unchanged);
        Assert.Equal(1, edited.Unchanged);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change WHERE field = 'assignee' AND \"new\" = 'user-bo'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_second_commit_replaces_the_comments_and_keeps_only_the_newest_50()
    {
        _ = await Commit(1, Write(Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("old", "gone", minutes: -5))));
        var many = Enumerable.Range(0, 60).Select(i => Comment("c" + i.ToString("D2", CultureInfo.InvariantCulture), "body " + i.ToString(CultureInfo.InvariantCulture), minutes: 100 - i)).ToArray();

        _ = await Commit(2, Write(Rows(Fields(Ada, Todo, RecordSamples.Now.AddMinutes(1)), LinearWrittenFields.None, many), version: "v2"));

        Assert.Equal(50, _world.Count($"SELECT count(*) FROM linear_comment WHERE issue_key = '{Key}'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_comment WHERE id = 'old'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_comment WHERE id = 'c00'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_comment WHERE id = 'c55'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Change_rows_older_than_30_days_are_deleted_by_the_next_commit()
    {
        _ = await Commit(1, Write(Rows(Fields(Ada, Todo))));
        _ = await Commit(2, Write(Rows(Fields(Bo, Todo, RecordSamples.Now.AddMinutes(1))), version: "v2"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change"));

        _clock.Advance(TimeSpan.FromDays(31));
        _ = await Commit(3, Write(Rows(Fields(Ada, Todo, RecordSamples.Now.AddDays(31))), version: "v3"));

        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change WHERE field = 'assignee' AND \"new\" = 'user-bo'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change WHERE field = 'assignee' AND \"new\" = 'user-ada'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Removing_the_issue_removes_its_rows()
    {
        _ = await Commit(1, Write(Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("c1", "hello"))));
        _ = await Commit(2, Write(Rows(Fields(Bo, Todo, RecordSamples.Now.AddMinutes(1))), version: "v2"));

        var removed = await Remove(3);

        Assert.Equal(1, removed.Tombstoned);
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_comment"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_issue_that_comes_back_after_a_tombstone_logs_no_change()
    {
        _ = await Commit(1, Write(Rows(Fields(Ada, Todo))));
        _ = await Remove(2);

        _ = await Commit(3, Write(Rows(Fields(Bo, Doing, RecordSamples.Now.AddMinutes(1))), version: "v2"));

        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_record_with_no_side_rows_leaves_the_stored_ones_alone()
    {
        _ = await Commit(1, Write(Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("c1", "hello"))));

        _ = await Commit(2, Write(side: null, version: "v2"));

        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_comment"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_default_writers_include_the_linear_one()
    {
        var store = new SqliteRecordStore(_world.Database);

        var committed = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, Write(Rows(Fields(Ada, Todo)))), Ct);

        Assert.False(committed.IsError);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_description_is_stored_and_a_changed_one_is_rewritten_on_an_unchanged_record()
    {
        _ = await Commit(1, Write(Described(Rows(Fields(Ada, Todo)), "first words")));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{Key}' AND description = 'first words'"));

        var same = await Commit(2, Write(Described(Rows(Fields(Ada, Todo)), "first words")));
        var edited = await Commit(3, Write(Described(Rows(Fields(Ada, Todo)), "second words")));

        Assert.Equal(1, same.Unchanged);
        Assert.Equal(1, edited.Unchanged);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{Key}' AND description = 'second words'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_description_is_stored_as_null_and_a_dropped_one_is_cleared()
    {
        _ = await Commit(1, Write(Described(Rows(Fields(Ada, Todo)), "words")));

        _ = await Commit(2, Write(Described(Rows(Fields(Ada, Todo)), description: null)));

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{Key}' AND description IS NULL"));
    }
}
