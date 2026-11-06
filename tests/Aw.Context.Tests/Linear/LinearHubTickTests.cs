using System.Text.Json.Nodes;
using Aw.Context.Core;

namespace Aw.Context.Tests.Linear;

/// <summary>What an index tick over the stateful fake Linear stores for the hub (Linear hub spec 2026-11-04 §4.1, §4.2): fields and comments through the side-row writer, the pickers' lists through the lookups step, and the one-time re-read.</summary>
[Trait("Lane", "integration")]
public sealed class LinearHubTickTests : IAsyncLifetime
{
    private LinearHubWorld _world = null!;
    private string _id = "";
    private string _key = "";

    public async ValueTask InitializeAsync()
    {
        _world = await LinearHubWorld.Start(seed => seed
            .Team("ENG").Team("OPS")
            .Member("Ada Fixture").Member("Bo Fixture")
            .Label("bug").Label("ops-only", "OPS")
            .Issue("ENG", "Fix the login timeout", new Testing.Linear.FakeLinearIssueSeed { Description = "Details", Assignee = "Ada Fixture", Priority = 2 })
            .Comment("ENG-1", "first comment"));
        _id = _world.Linear.Issues.Single().Id;
        _key = "linear:issue/" + _id;
    }

    public ValueTask DisposeAsync() => _world.DisposeAsync();

    [Fact]
    public async Task A_tick_stores_the_lookups_of_the_profiles_team_and_not_another_teams()
    {
        var row = await _world.Tick();

        Assert.Null(row.Code);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_team WHERE key = 'ENG' AND id = '{_world.Linear.TeamId("ENG")}'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_team WHERE key = 'OPS'"));
        Assert.Equal(5, _world.Count($"SELECT count(*) FROM linear_state WHERE team_id = '{_world.Linear.TeamId("ENG")}'"));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_state WHERE name = 'In Progress' AND type = 'started' AND id = '{_world.Linear.StateId("ENG", "In Progress")}'"));
        // the fake gives every team all its members, the seeded viewer among them
        Assert.Equal(3, _world.Count($"SELECT count(*) FROM linear_member WHERE team_id = '{_world.Linear.TeamId("ENG")}'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_label WHERE name = 'bug'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_label WHERE name = 'ops-only'"));
    }

    [Fact]
    public async Task A_second_tick_replaces_the_lookups_so_a_dropped_member_is_gone()
    {
        _ = await _world.Tick();
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_label WHERE name = 'bug'"));
        _world.Sql($"INSERT INTO linear_label (team_id, id, name) VALUES ('{_world.Linear.TeamId("ENG")}', 'stale-label', 'stale')");

        _ = await _world.Tick();

        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_label WHERE name = 'stale'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_label WHERE name = 'bug'"));
    }

    [Fact]
    public async Task The_lookups_refresh_on_a_tick_that_ended_at_its_time_cap()
    {
        _world.OnRequest = body =>
        {
            if (body.Contains("IssuesPull", StringComparison.Ordinal))
            {
                _world.Clock.Advance(TimeSpan.FromMinutes(11));   // the first page took longer than the tick's cap
            }
        };

        var row = await _world.Tick();

        Assert.Equal(ContextErrors.SyncCapped.Code, row.Code);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}'"));
        Assert.Equal(5, _world.Count($"SELECT count(*) FROM linear_state WHERE team_id = '{_world.Linear.TeamId("ENG")}'"));
    }

    [Fact]
    public async Task A_stored_f2_issue_is_re_read_once_and_the_re_save_logs_no_change()
    {
        _ = await _world.Tick();
        // an issue as ';f2' left it: its record at the old version and no side rows, and the cursor back at the start of Recent as migration 0032 puts it
        _world.Sql($"UPDATE source_record SET version = replace(version, ';f3', ';f2') WHERE key = '{_key}'");
        _world.Sql($"DELETE FROM linear_issue WHERE key = '{_key}'");
        _world.Sql("UPDATE source_cursor SET phase = 'recent', cursor = NULL, backfill_cursor = NULL, backfill_before = NULL WHERE source = 'linear' AND repo = 'records'");

        var again = await _world.Tick();
        var quiet = await _world.Tick();

        Assert.Equal(1, again.Records);
        Assert.Equal(0, quiet.Records);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}' AND priority = 2"));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_comment WHERE issue_key = '{_key}'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM source_record WHERE key = '{_key}' AND version LIKE '%;f3'"));
    }

    [Fact]
    public async Task The_first_re_save_after_the_migration_reset_logs_no_change_rows()
    {
        _ = await _world.Tick();
        // 0032's reset: the cursor goes back to the start of Recent; an issue already holds its side rows from an earlier tick, then Linear changes it
        _world.Sql("UPDATE source_cursor SET phase = 'recent', cursor = NULL, backfill_cursor = NULL, backfill_before = NULL WHERE source = 'linear' AND repo = 'records'");
        _world.Sql($"DELETE FROM linear_issue WHERE key = '{_key}'");
        await _world.Update(_id, new JsonObject { ["assigneeId"] = _world.Linear.MemberId("Bo Fixture"), ["stateId"] = _world.Linear.StateId("ENG", "Done") });

        _ = await _world.Tick();

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}' AND state_type = 'completed'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact]
    public async Task A_later_tick_logs_a_teammates_move_as_from_the_feed()
    {
        _ = await _world.Tick();
        await _world.Update(_id, new JsonObject { ["assigneeId"] = _world.Linear.MemberId("Bo Fixture") });

        _ = await _world.Tick();

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue_change WHERE issue_key = '{_key}' AND field = 'assignee' AND via = 'feed' AND \"new\" = '{_world.Linear.MemberId("Bo Fixture")}' AND \"old\" = '{_world.Linear.MemberId("Ada Fixture")}'"));
    }
}
