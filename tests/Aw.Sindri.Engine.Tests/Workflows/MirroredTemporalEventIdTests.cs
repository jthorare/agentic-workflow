using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// UI E-30, the engine half (spec §5.6): every row 'RecordEvent' mirrors records the Temporal event id it was written for in
/// the ledger's nullable 'temporal_event_id' column, the third part of the row's idempotency key; every other row has none; a
/// replay writes no second row.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class MirroredTemporalEventIdTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Every_mirrored_row_has_the_id_of_its_key_and_the_others_have_none_even_when_every_task_replays()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var other = sindri.GetRequiredService<EventFactory>().Entry("service.heartbeat", new WorkflowEventPayload("none", "a", "b", "tick", Outcome: null, Run: null), "not-mirrored");
        _ = await sindri.GetRequiredService<ILedgerStore>().Append(other, sindri.GetRequiredService<HeldLease>().Current!, ct);
        var options = sindri.GetRequiredService<TemporalWorkerOptions>();
        options.MaxCachedWorkflows = 0;   // every workflow task replays the history from the start
        using var worker = new TemporalWorker(env.Client, options);

        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.StepStart(sindri, "T-61", ct);
            var handle = await WorkflowsHarness.Start(env.Client, start);
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));
            var cancel = new CancelRequested("human:cli", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string('d', 32)), new string('d', 64)), Sequence: null, Parent: null);
            _ = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(cancel), new WorkflowUpdateOptions(new string('d', 32)));
        }, ct);

        var rows = Rows();
        var mirrored = rows.Where(static r => r.Type.StartsWith("workflow.", StringComparison.Ordinal)).ToList();
        Assert.Equal(["workflow.started", "workflow.transition", "workflow.transition"], mirrored.Select(static r => r.Type), StringComparer.Ordinal);   // the start, the desk report, the cancel: nothing twice, nothing missing
        Assert.All(mirrored, static r => Assert.False(string.IsNullOrEmpty(r.TemporalEventId), $"{r.Type} {r.Key}"));
        Assert.All(mirrored, static r => Assert.Equal(r.Key.Split(':')[^1], r.TemporalEventId));   // the column and the key never disagree
        Assert.Equal(mirrored.Count, mirrored.Select(static r => r.TemporalEventId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows.Where(static r => !r.Type.StartsWith("workflow.", StringComparison.Ordinal)), static r => Assert.Null(r.TemporalEventId));
        Assert.Contains(rows, static r => string.Equals(r.Type, "service.heartbeat", StringComparison.Ordinal));
    }

    [Fact(Timeout = 60_000)]
    public async Task The_column_is_indexed_with_the_instance_of_the_payload()
    {
        await using var sindri = _harness.Sindri();
        _ = await sindri.GetRequiredService<ILedgerStore>().Schema(TestContext.Current.CancellationToken);
        using var connection = new SqliteConnection($"Data Source={Path.Combine(_harness.Home, "ledger.db")};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'index' AND tbl_name = 'events' AND sql LIKE '%temporal_event_id%'";
        var sql = Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(sql);
        Assert.Contains("instance", sql, StringComparison.Ordinal);
    }

    private List<(string Type, string Key, string? TemporalEventId)> Rows()
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(_harness.Home, "ledger.db")};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT type, idempotency_key, temporal_event_id FROM events ORDER BY seq";
        using var reader = command.ExecuteReader();
        List<(string, string, string?)> rows = [];
        while (reader.Read())
        {
            rows.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }
}
