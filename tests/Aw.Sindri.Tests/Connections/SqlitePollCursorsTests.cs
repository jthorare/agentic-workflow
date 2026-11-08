using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Connections.Polling;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The 'poll_cursor' table of ledger.db (migration 10) on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqlitePollCursorsTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<LedgerDatabase> _databases = [];

    public void Dispose()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
        }

        _home.Dispose();
    }

    [Fact]
    public async Task Save_and_read_per_connection_and_event_kind_with_backoff_until_kept()
    {
        var ct = TestContext.Current.CancellationToken;
        var cursors = Create();
        var labeled = Row("linear", EventKind.LinearIssueLabeled, "c1");
        var created = Row("linear", EventKind.LinearIssueCreated, "c2") with { BackoffUntil = KnownInstants.Epoch.AddMinutes(2), Failures = 2 };
        var merged = Row("github", EventKind.GitHubPrMerged, "c3");

        foreach (var row in new[] { labeled, created, merged })
        {
            Assert.True((await cursors.Save(row, ct)).IsSuccess);
        }

        Assert.Equal(labeled, (await cursors.Get(labeled.Connection, labeled.Kind, ct)).Value);
        Assert.Equal(created, (await cursors.Get(created.Connection, created.Kind, ct)).Value);
        Assert.Equal(merged, (await cursors.Get(merged.Connection, merged.Kind, ct)).Value);
        var listed = (await cursors.List(ct)).Value;
        Assert.Equal(
            ["github:work github.pr.merged", "linear:work linear.issue.created", "linear:work linear.issue.labeled"],
            listed.Select(static c => $"{c.Connection} {c.Kind}"),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_second_save_replaces_the_row_and_clears_the_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var cursors = Create();
        _ = await cursors.Save(Row("linear", EventKind.LinearIssueLabeled, "c1") with { BackoffUntil = KnownInstants.Epoch.AddMinutes(5), Failures = 3 }, ct);

        var healed = Row("linear", EventKind.LinearIssueLabeled, "c9");
        _ = await cursors.Save(healed, ct);

        var read = (await cursors.Get(healed.Connection, healed.Kind, ct)).Value;
        Assert.Equal(healed, read);
        Assert.Null(read!.BackoffUntil);
        Assert.Equal(0, read.Failures);
        _ = Assert.Single((await cursors.List(ct)).Value);
    }

    [Fact]
    public async Task A_cursor_nobody_saved_is_null()
    {
        var cursors = Create();

        var read = await cursors.Get(ConnectionSamples.Name(), EventKind.LinearIssueLabeled, TestContext.Current.CancellationToken);

        Assert.True(read.IsSuccess);
        Assert.Null(read.Value);
    }

    [Fact]
    public void The_migration_that_makes_the_table_is_named_poll_cursors()
        => Assert.Contains(LedgerDatabase.Migrations, static m => m.Version == 10 && m.Name.Contains("poll_cursors", StringComparison.Ordinal));

    private static PollCursor Row(string service, EventKind kind, string value) => new(
        ConnectionSamples.Name(service, "work"),
        kind,
        value,
        UpdatedAt: KnownInstants.Epoch.AddMinutes(1),
        LastPollAt: KnownInstants.Epoch.AddMinutes(2),
        BackoffUntil: null,
        Failures: 0);

    private SqlitePollCursors Create()
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
        _databases.Add(database);
        return new SqlitePollCursors(database);
    }
}
