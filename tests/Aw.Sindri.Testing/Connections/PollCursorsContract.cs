using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Xunit;

namespace Aw.Sindri.Testing.Connections;

/// <summary>
/// The contract every <see cref="IPollCursors"/> honours: a saved row reads back whole per (connection, event kind), a second save replaces it
/// (backoff and failures too), a cursor nobody saved is null, and list is sorted by connection then kind.
/// </summary>
public abstract class PollCursorsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IPollCursors Create();

    /// <summary>Rows are kept apart by connection and event kind, with their backoff.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Rows_are_kept_per_connection_and_event_kind_with_the_backoff()
    {
        var cursors = Create();
        var labeled = Row("linear", EventKind.LinearIssueLabeled, "c1");
        var created = Row("linear", EventKind.LinearIssueCreated, "c2") with { BackoffUntil = KnownInstants.Epoch.AddMinutes(2), Failures = 2 };
        var merged = Row("github", EventKind.GitHubPrMerged, "c3");
        foreach (var row in new[] { labeled, created, merged })
        {
            Expect.True((await cursors.Save(row, Token)).IsSuccess);
        }

        Expect.Equal(labeled, (await cursors.Get(labeled.Connection, labeled.Kind, Token)).Value);
        Expect.Equal(created, (await cursors.Get(created.Connection, created.Kind, Token)).Value);
        Expect.Equal(merged, (await cursors.Get(merged.Connection, merged.Kind, Token)).Value);
        Expect.Sequence(
            ["github:work github.pr.merged", "linear:work linear.issue.created", "linear:work linear.issue.labeled"],
            (await cursors.List(Token)).Value.Select(static c => $"{c.Connection} {c.Kind}"));
    }

    /// <summary>A second save replaces the row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_second_save_replaces_the_row_and_clears_the_backoff()
    {
        var cursors = Create();
        _ = await cursors.Save(Row("linear", EventKind.LinearIssueLabeled, "c1") with { BackoffUntil = KnownInstants.Epoch.AddMinutes(5), Failures = 3 }, Token);
        var healed = Row("linear", EventKind.LinearIssueLabeled, "c9");

        _ = await cursors.Save(healed, Token);

        Expect.Equal(healed, (await cursors.Get(healed.Connection, healed.Kind, Token)).Value);
        _ = Expect.Only((await cursors.List(Token)).Value);
    }

    /// <summary>A cursor nobody saved is null.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_cursor_nobody_saved_is_null()
    {
        var read = await Create().Get(ConnectionSamples.Name(), EventKind.LinearIssueLabeled, Token);

        Expect.True(read.IsSuccess);
        Expect.True(read.Value is null);
    }

    private static PollCursor Row(string service, EventKind kind, string value) => new(
        ConnectionSamples.Name(service, "work"),
        kind,
        value,
        UpdatedAt: KnownInstants.Epoch.AddMinutes(1),
        LastPollAt: KnownInstants.Epoch.AddMinutes(2),
        BackoffUntil: null,
        Failures: 0);
}
