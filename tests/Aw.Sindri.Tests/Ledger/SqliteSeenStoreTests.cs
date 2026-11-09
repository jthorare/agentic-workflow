using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Tests.Graph.Blocks;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>'workflow_seen' in ledger.db (migration 14) on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteSeenStoreTests : IDisposable
{
    private static readonly WorkflowId _a = WorkflowId.From(FlowFakes.WorkflowA);
    private static readonly WorkflowId _b = WorkflowId.From(FlowFakes.WorkflowB);

    private readonly TempHome _home = new();
    private readonly LedgerDatabase _database;
    private readonly SqliteSeenStore _store;

    public SqliteSeenStoreTests()
    {
        _database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
        _store = new SqliteSeenStore(_database, new FakeTimeProvider(KnownInstants.Epoch));
    }

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void The_migration_that_makes_the_table_is_number_14_named_workflow_seen_and_reads_start_there()
    {
        Assert.Contains(LedgerDatabase.Migrations, static m => m.Version == 14 && m.Name.Contains("workflow_seen", StringComparison.Ordinal));
        Assert.Equal(14, LedgerDatabase.WorkflowSeenVersion);
    }

    [Fact]
    public async Task A_read_of_a_file_that_was_never_written_finds_nothing_seen()
    {
        var seen = await _store.Seen(_a, ["k1"], Token);

        Assert.True(seen.IsSuccess);
        Assert.Empty(seen.Value);
    }

    [Fact]
    public async Task Recorded_keys_are_seen_by_their_workflow_only_and_recording_twice_is_harmless()
    {
        Assert.True((await _store.Record(_a, ["k1", "k2"], Token)).IsSuccess);
        Assert.True((await _store.Record(_a, ["k2", "k3"], Token)).IsSuccess);

        var seenByA = await _store.Seen(_a, ["k1", "k2", "k3", "k4"], Token);
        var seenByB = await _store.Seen(_b, ["k1"], Token);

        Assert.Equal(["k1", "k2", "k3"], seenByA.Value.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Empty(seenByB.Value);
    }

    [Fact]
    public async Task Keys_with_odd_characters_and_an_empty_list_are_kept_exactly()
    {
        Assert.True((await _store.Record(_a, ["it's \"quoted\"", "ünï", ""], Token)).IsSuccess);

        var seen = await _store.Seen(_a, ["it's \"quoted\"", "ünï", "", "other"], Token);

        Assert.Equal(3, seen.Value.Count);
        Assert.True((await _store.Record(_a, [], Token)).IsSuccess);
    }
}
