using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;
using MapWorld = Aw.Context.Tests.Map.MapWorld;

namespace Aw.Context.Tests.Sync;

/// <summary>A stored value a row mapper refuses leaves the store as a StoredRowException, whichever mapper reads it (#2229), so a host answers a store fault and not a bad request.</summary>
[Trait("Lane", "integration")]
public sealed class StoredRowFaultTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Item() => new(
        RecordKey.From("linear:item/1"), RecordKind.Issue, "Title 1", "Words of linear item 1", Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, RecordSamples.Now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("linear:team-a"));

    private static void Update(TempHome home, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={home.Settings.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        Assert.Equal(expected: 1, command.ExecuteNonQuery());
    }

    [Fact]
    public async Task A_bad_stored_phase_read_by_the_position_mapper_is_a_StoredRowException_not_a_bare_parse_exception()
    {
        using var home = new TempHome();
        var store = new SqliteRecordStore(new ContextDatabase(home.Settings, home.Clock));
        var sync = new IndexSync(new RecordFeedSync(new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Linear, [Item()])), store, home.Clock), store, new FakeScrubberFactory());
        Assert.True((await sync.Tick(MapWorld.Human, Ct)).IsSuccess);
        Update(home, "UPDATE source_cursor SET phase = 'no-such-phase' WHERE repo = 'records'");

        var thrown = await Assert.ThrowsAsync<StoredRowException>(async () => await store.Position(RecordSamples.Linear, Ct));

        Assert.DoesNotContain("no-such-phase", thrown.Message, StringComparison.Ordinal);
        Assert.NotNull(thrown.InnerException);
    }

    [Fact]
    public async Task A_bad_stored_repo_names_source_record_repo_and_the_key_but_not_the_value()
    {
        using var home = new TempHome();
        var store = new SqliteRecordStore(new ContextDatabase(home.Settings, home.Clock));
        var sync = new IndexSync(new RecordFeedSync(new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Linear, [Item()])), store, home.Clock), store, new FakeScrubberFactory());
        Assert.True((await sync.Tick(MapWorld.Human, Ct)).IsSuccess);
        Update(home, "UPDATE source_record SET repo = 'acme/api' WHERE key = 'linear:item/1'");

        var thrown = await Assert.ThrowsAsync<StoredRowException>(async () => await store.Get(RecordKey.From("linear:item/1"), Ct));

        Assert.Equal("source_record", thrown.Table);
        Assert.Equal("repo", thrown.Column);
        Assert.Equal("linear:item/1", thrown.RowKey);
        Assert.DoesNotContain("acme/api", thrown.Message, StringComparison.Ordinal);
    }
}
