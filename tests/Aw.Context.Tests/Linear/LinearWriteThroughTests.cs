using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.LinearWriteThrough;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Linear;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The write-through (Linear hub spec 2026-11-04 D4) over the stateful fake Linear and the real store: the issue is re-read with the feed's query and mapper and committed as a batch of one with no position and the guard,
/// taking no lock, so it lands while a tick holds the source's and a page fetched before it cannot revert it.
/// </summary>
[Trait("Lane", "integration")]
public sealed class LinearWriteThroughTests : IAsyncLifetime
{
    // why: built from two halves so the file holds no string a secret scanner flags; the redaction rules match it as an AWS access key id
    private const string Secret = "AKIA" + "IOSFODNN7" + "EXAMPLE";

    private LinearHubWorld _world = null!;
    private string _id = "";
    private string _key = "";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _world = await LinearHubWorld.Start(seed => seed
            .Team("ENG").Team("OPS")
            .Member("Ada Fixture").Member("Bo Fixture")
            .Label("bug")
            .Issue("ENG", "Fix the login timeout", new FakeLinearIssueSeed { Description = "Details of the timeout", State = "Todo", Assignee = "Ada Fixture", Priority = 2, Estimate = 3, Labels = ["bug"] })
            .Issue("OPS", "Rotate the keys")
            .Comment("ENG-1", "first comment"));
        _id = _world.Linear.Issues.Single(static i => i.Team == "ENG").Id;
        _key = "linear:issue/" + _id;
    }

    public ValueTask DisposeAsync() => _world.DisposeAsync();

    private IRecordStore Store => _world.Provider.GetRequiredService<IRecordStore>();

    private async Task<Result<LinearWriteThroughResponse>> WriteThrough(string? issueId = null, LinearWrittenFields written = LinearWrittenFields.None)
    {
        await using var scope = _world.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IService<LinearWriteThroughRequest, LinearWriteThroughResponse>>();
        return await service.Handle(new LinearWriteThroughRequest(_world.Scope, ExternalId.From(issueId ?? _id), written), Ct);
    }

    [Fact]
    public async Task A_tick_stores_the_issues_fields_and_comments_and_logs_no_change()
    {
        var row = await _world.Tick();

        Assert.Null(row.Code);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}' AND identifier = 'ENG-1' AND state_type = 'unstarted' AND priority = 2 AND estimate = 3 AND assignee_id = '{_world.Linear.MemberId("Ada Fixture")}' AND team_id = '{_world.Linear.TeamId("ENG")}' AND labels = '[\"{_world.Linear.LabelId("bug")}\"]'"));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_comment WHERE issue_key = '{_key}' AND body = 'first comment'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact]
    public async Task A_write_through_stores_the_new_fields_and_logs_the_field_the_write_sent_as_write_through()
    {
        _ = await _world.Tick();
        await _world.Update(_id, new JsonObject { ["stateId"] = _world.Linear.StateId("ENG", "Done"), ["title"] = "Fixed the login timeout" });

        var stored = await WriteThrough(written: LinearWrittenFields.State);

        Assert.True(stored.IsSuccess, stored.IsError ? stored.Error.Message : "");
        Assert.Equal(_key, stored.Value.Key?.Value);
        Assert.True(stored.Value.Written);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}' AND state_type = 'completed' AND state_id = '{_world.Linear.StateId("ENG", "Done")}'"));
        Assert.Equal("Fixed the login timeout", _world.Text($"SELECT title FROM source_record WHERE key = '{_key}'"));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue_change WHERE issue_key = '{_key}' AND field = 'state' AND via = 'write-through' AND \"old\" = '{_world.Linear.StateId("ENG", "Todo")}'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }

    [Fact]
    public async Task A_write_through_does_not_move_the_feeds_stored_position()
    {
        _ = await _world.Tick();
        var before = (await Store.Position(LinearSourceIds.Issues, Ct)).Value;
        await _world.Update(_id, new JsonObject { ["priority"] = 4 });

        _ = await WriteThrough();

        Assert.Equal(before, (await Store.Position(LinearSourceIds.Issues, Ct)).Value);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}' AND priority = 4"));
    }

    [Fact]
    public async Task A_teammates_state_move_absorbed_by_the_owners_priority_save_is_logged_as_from_the_feed()
    {
        _ = await _world.Tick();
        await _world.Update(_id, new JsonObject { ["stateId"] = _world.Linear.StateId("ENG", "In Progress") });   // a teammate moves it; the feed has not seen it
        await _world.Update(_id, new JsonObject { ["priority"] = 1 });   // the owner saves a priority

        _ = await WriteThrough();

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue_change WHERE issue_key = '{_key}' AND field = 'state' AND via = 'feed'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change WHERE via = 'write-through'"));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}' AND priority = 1 AND state_type = 'started'"));
    }

    [Fact]
    public async Task A_write_through_while_a_tick_holds_the_source_lock_lands()
    {
        _ = await _world.Tick();
        await _world.Update(_id, new JsonObject { ["priority"] = 4 });
        var feeds = _world.Provider.GetRequiredService<RecordFeedSync>();
        Result<LinearWriteThroughResponse>? during = null;

        var held = await feeds.Locked(LinearSourceIds.Issues, async () =>
        {
            during = await WriteThrough();
            return new SourceSync(LinearSourceIds.Issues, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Code: null);
        });

        Assert.True(held.IsSuccess);
        Assert.NotNull(during);
        Assert.True(during.IsSuccess, during.IsError ? during.Error.Message : "");
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}' AND priority = 4"));
    }

    [Fact]
    public async Task A_page_fetched_before_the_write_does_not_put_the_old_value_back()
    {
        _ = await _world.Tick();
        var settings = LinearSettings.Create(_world.Linear.Url.ToString(), "env:" + LinearHubWorld.KeyName, ["ENG"]).Value;
        var feed = _world.Sources.CreateFeed(settings);
        var late = (await feed.Pull(new RecordPull(After: null, Limit: 20, NotBefore: LinearHubWorld.Now.AddDays(-30)), Ct)).Value;   // the tick read the issue at priority 2
        await _world.Update(_id, new JsonObject { ["priority"] = 4 });
        _ = await WriteThrough();

        var scrubbers = new FakeScrubberFactory().For([]).Value;
        var batch = RecordBatches.From(LinearSourceIds.Issues, late, SyncPhase.Incremental, scrubbers, LinearHubWorld.Now) with { Guard = feed.Guard };
        var committed = await Store.Commit(batch, Ct);

        Assert.True(feed.Guard);
        Assert.Equal(0, committed.Value.Written);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}' AND priority = 4"));
    }

    [Fact]
    public async Task A_purge_holding_the_source_skips_the_write_through_with_SND_LINEAR_013()
    {
        _ = await _world.Tick();
        await _world.Update(_id, new JsonObject { ["priority"] = 4 });
        var feeds = _world.Provider.GetRequiredService<RecordFeedSync>();
        Result<LinearWriteThroughResponse>? during = null;

        _ = await feeds.Exclusive(LinearSourceIds.Issues, async () =>
        {
            during = await WriteThrough();
            return Results.Success(0);
        });

        Assert.NotNull(during);
        Assert.True(during.IsError);
        Assert.Equal("SND-LINEAR-013", during.Error.Code);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue WHERE key = '{_key}' AND priority = 2"));
        Assert.True((await WriteThrough()).IsSuccess);   // the purge is over
    }

    [Fact]
    public async Task A_write_through_of_an_unchanged_issue_stores_what_the_tick_stored()
    {
        _ = await _world.Tick();
        var hash = _world.Text($"SELECT text_hash FROM source_record WHERE key = '{_key}'");
        var chunks = _world.Count($"SELECT count(*) FROM chunk WHERE record = '{_key}'");

        var stored = await WriteThrough();

        Assert.True(stored.IsSuccess, stored.IsError ? stored.Error.Message : "");
        Assert.False(stored.Value.Written);
        Assert.Equal(hash, _world.Text($"SELECT text_hash FROM source_record WHERE key = '{_key}'"));
        Assert.Equal(chunks, _world.Count($"SELECT count(*) FROM chunk WHERE record = '{_key}'"));
    }

    [Fact]
    public async Task ASecret_in_the_description_and_in_a_comment_is_scrubbed_before_it_is_stored()
    {
        _ = await _world.Tick();
        await _world.Update(_id, new JsonObject { ["description"] = $"Use the key {Secret} to reach the bucket." });
        await _world.Mutate(
            "mutation M($input: CommentCreateInput!) { commentCreate(input: $input) { success } }",
            new JsonObject { ["input"] = new JsonObject { ["issueId"] = _id, ["body"] = $"I used {Secret} yesterday." } });

        _ = await WriteThrough();

        Assert.Equal(0, _world.Count($"SELECT count(*) FROM chunk WHERE text LIKE '%{Secret}%'"));
        Assert.Equal(0, _world.Count($"SELECT count(*) FROM linear_comment WHERE body LIKE '%{Secret}%'"));
        Assert.Equal(2, _world.Count($"SELECT count(*) FROM linear_comment WHERE issue_key = '{_key}'"));
        Assert.True(_world.Count($"SELECT count(*) FROM chunk WHERE record = '{_key}'") >= 1);
    }

    [Fact]
    public async Task An_issue_outside_the_profiles_teams_is_not_stored()
    {
        _ = await _world.Tick();
        var other = _world.Linear.Issues.Single(static i => i.Team == "OPS").Id;

        var stored = await WriteThrough(other);

        Assert.True(stored.IsSuccess, stored.IsError ? stored.Error.Message : "");
        Assert.Null(stored.Value.Key);
        Assert.False(stored.Value.Written);
        Assert.Equal(0, _world.Count($"SELECT count(*) FROM source_record WHERE key = 'linear:issue/{other}'"));
    }

    [Fact]
    public async Task A_profile_without_linear_is_refused()
    {
        await using var scope = _world.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IService<LinearWriteThroughRequest, LinearWriteThroughResponse>>();
        var bare = _world.Scope with { Settings = System.Text.Json.JsonDocument.Parse("""{"sources":{}}""").RootElement.Clone() };

        var stored = await service.Handle(new LinearWriteThroughRequest(bare, ExternalId.From(_id), LinearWrittenFields.None), Ct);

        Assert.True(stored.IsError);
        Assert.Equal(ContextErrors.SourceNotConfigured.Code, stored.Error.Code);
    }
}
