using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'RecordEvent' under a workflow-task replay (review run 2, data-integrity-guardian-r2-0): the same key with the same event
/// records once; the same key with a different event (the replay saw more signals) records under '{key}:{content hash}'.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RecordEventReplayTests
{
    private const string Key = "workflow:T-1/step/1:run-1:event:3";

    [Fact(Timeout = 30_000)]
    public async Task The_same_event_under_the_same_key_records_once()
    {
        var (activities, ledger) = await Open();
        var ct = TestContext.Current.CancellationToken;

        var first = await activities.Record(Moved("checks"), Key, "event:3", ct);
        var again = await activities.Record(Moved("checks"), Key, "event:3", ct);

        Assert.Equal((false, true, first.Sequence), (first.AlreadyRecorded, again.AlreadyRecorded, again.Sequence));
        _ = Assert.Single((await ledger.ReadAfter(0, 100, ct)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_different_event_under_a_taken_key_records_under_the_key_and_a_content_hash()
    {
        var (activities, ledger) = await Open();
        var ct = TestContext.Current.CancellationToken;

        _ = await activities.Record(Moved("checks"), Key, "event:3", ct);
        var other = await activities.Record(Moved("produce") with { Trigger = "answer" }, Key, "event:3", ct);
        var rows = (await ledger.ReadAfter(0, 100, ct)).Value;

        Assert.False(other.AlreadyRecorded);
        Assert.Equal(2, rows.Count);
        Assert.Equal(Key, rows[0].IdempotencyKey);
        Assert.Matches("^" + Key + ":[0-9a-f]{12}$", rows[1].IdempotencyKey);
    }

    [Fact(Timeout = 30_000)]
    public async Task Replays_that_differ_only_in_their_outcome_each_keep_their_own_row()
    {
        var (activities, ledger) = await Open();
        var ct = TestContext.Current.CancellationToken;

        _ = await activities.Record(Moved("checks"), Key, "event:3", ct);
        _ = await activities.Record(Moved("checks") with { Outcome = "failed" }, Key, "event:3", ct);
        _ = await activities.Record(Moved("checks") with { Outcome = "retry" }, Key, "event:3", ct);
        var rows = (await ledger.ReadAfter(0, 100, ct)).Value;

        Assert.Equal(3, rows.Count);
        Assert.Equal(3, rows.Select(static r => r.IdempotencyKey).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact(Timeout = 30_000)]
    public async Task A_failed_ledger_read_does_not_mean_the_event_is_the_same_so_the_retry_lands_it_under_a_content_hash()
    {
        ReadFailsOnce? flaky = null;
        var (activities, ledger) = await Open(inner => flaky = new ReadFailsOnce(inner));
        var ct = TestContext.Current.CancellationToken;
        _ = await activities.Record(Moved("checks"), Key, "event:3", ct);

        flaky!.Arm();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => activities.Record(Moved("produce") with { Trigger = "answer" }, Key, "event:3", ct));
        var retried = await activities.Record(Moved("produce") with { Trigger = "answer" }, Key, "event:3", ct);
        var rows = (await ledger.ReadAfter(0, 100, ct)).Value;

        Assert.False(retried.AlreadyRecorded);
        Assert.Equal(2, rows.Count);
        Assert.Matches("^" + Key + ":[0-9a-f]{12}$", rows[1].IdempotencyKey);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_secret_in_the_detail_is_scrubbed_before_the_row_is_appended_and_a_replay_still_matches_it()
    {
        var (activities, ledger) = await Open();
        var ct = TestContext.Current.CancellationToken;
        var secret = "ghp_" + new string('a', 36);
        var bare = WorkflowEventDetail.Of(new DeadlineFired("service:workflow.timers", 1, DeadlineKind.State), WorkflowInstanceId.Of(InstanceAddress.From("T-1/step/1")), suspension: null);
        var detail = bare with { Reason = $"the step failed with {secret}" };
        var record = Moved("checks") with { Detail = detail };

        var first = await activities.Record(record, Key, "event:3", ct);
        var again = await activities.Record(record, Key, "event:3", ct);
        var stored = Assert.Single((await ledger.ReadAfter(0, 100, ct)).Value).Envelope.Payload;

        Assert.DoesNotContain(secret, stored, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:github-token]", stored, StringComparison.Ordinal);
        Assert.Equal((false, true), (first.AlreadyRecorded, again.AlreadyRecorded));
        _ = System.Text.Json.JsonDocument.Parse(stored);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_blocked_scrub_stores_one_valid_json_row_without_its_detail_and_a_replay_matches_it()
    {
        var (activities, ledger) = await Open(redactor: new StubRedactor(static text => text.Contains("secret-reason", StringComparison.Ordinal) ? _blocked : Pass(text)));
        var ct = TestContext.Current.CancellationToken;
        var record = Moved("checks") with { Detail = WithReason("secret-reason") };

        var first = await activities.Record(record, Key, "event:3", ct);
        var again = await activities.Record(record, Key, "event:3", ct);
        var rows = (await ledger.ReadAfter(0, 100, ct)).Value;

        var row = Assert.Single(rows);
        using var json = System.Text.Json.JsonDocument.Parse(row.Envelope.Payload);
        Assert.False(json.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind != System.Text.Json.JsonValueKind.Null);
        Assert.DoesNotContain("secret-reason", row.Envelope.Payload, StringComparison.Ordinal);
        Assert.Equal((false, true, first.Sequence), (first.AlreadyRecorded, again.AlreadyRecorded, again.Sequence));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_scrub_result_that_is_not_json_stores_the_row_without_its_detail()
    {
        var (activities, ledger) = await Open(redactor: new StubRedactor(static text => text.Contains("secret-reason", StringComparison.Ordinal) ? new ScrubResult("{ not json", [], ScrubOutcome.Redacted, BlockCode: null) : Pass(text)));
        var ct = TestContext.Current.CancellationToken;

        _ = await activities.Record(Moved("checks") with { Detail = WithReason("secret-reason") }, Key, "event:3", ct);
        var row = Assert.Single((await ledger.ReadAfter(0, 100, ct)).Value);

        using var json = System.Text.Json.JsonDocument.Parse(row.Envelope.Payload);
        Assert.False(json.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind != System.Text.Json.JsonValueKind.Null);
        Assert.DoesNotContain("secret-reason", row.Envelope.Payload, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_dropped_detail_is_warned_about_with_the_key_and_the_block_code_and_never_the_text()
    {
        var log = new ListLogger<RecordEventActivities>();
        var (activities, _) = await Open(redactor: new StubRedactor(static text => text.Contains("secret-reason", StringComparison.Ordinal) ? _blocked : Pass(text)), logger: log);

        _ = await activities.Record(Moved("checks") with { Detail = WithReason("secret-reason") }, Key, "event:3", TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Contains(Key, line, StringComparison.Ordinal);
        Assert.Contains(RedactionCodes.MatchTimeout, line, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-reason", line, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_dropped_detail_whose_scrub_was_not_json_says_so_and_never_the_text()
    {
        var log = new ListLogger<RecordEventActivities>();
        var (activities, _) = await Open(redactor: new StubRedactor(static text => text.Contains("secret-reason", StringComparison.Ordinal) ? new ScrubResult("{ not json", [], ScrubOutcome.Redacted, BlockCode: null) : Pass(text)), logger: log);

        _ = await activities.Record(Moved("checks") with { Detail = WithReason("secret-reason") }, Key, "event:3", TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Contains(Key, line, StringComparison.Ordinal);
        Assert.Contains("not json", line, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-reason", line, StringComparison.Ordinal);
        Assert.DoesNotContain("{ not json", line, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_bare_payload_that_still_cannot_be_scrubbed_throws_so_the_activity_retries_and_nothing_is_stored()
    {
        var (activities, ledger) = await Open(redactor: new StubRedactor(static _ => _blocked));
        var ct = TestContext.Current.CancellationToken;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => activities.Record(Moved("checks"), Key, "event:3", ct));

        Assert.Contains("can't be scrubbed", thrown.Message, StringComparison.Ordinal);
        Assert.Empty((await ledger.ReadAfter(0, 100, ct)).Value);
    }

    private static readonly ScrubResult _blocked = new(ScrubResult.BlockedText, [], ScrubOutcome.Blocked, BlockCode: RedactionCodes.MatchTimeout);

    private static ScrubResult Pass(string text) => new(text, [], ScrubOutcome.Clean, BlockCode: null);

    private static WorkflowEventDetail WithReason(string reason) =>
        WorkflowEventDetail.Of(new DeadlineFired("service:workflow.timers", 1, DeadlineKind.State), WorkflowInstanceId.Of(InstanceAddress.From("T-1/step/1")), suspension: null) with { Reason = reason };

    /// <summary>A redactor that answers with the given function.</summary>
    private sealed class StubRedactor(Func<string, ScrubResult> scrub) : IRedactor
    {
        public ScrubResult Scrub(string text) => scrub(text);

        public bool IsSecretName(string name) => false;
    }

    /// <summary>The real ledger, except that once armed its next read fails (a busy file).</summary>
    private sealed class ReadFailsOnce(ILedgerStore inner) : ILedgerStore
    {
        private bool _armed;

        public void Arm() => _armed = true;

        public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken) => inner.Append(entry, grant, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken)
        {
            if (!_armed)
            {
                return inner.ReadAfter(sequence, max, cancellationToken);
            }

            _armed = false;
            return ValueTask.FromResult<Result<IReadOnlyList<LedgerEvent>>>(ReadFailingLedger.Failure);
        }

        public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

        public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => inner.Head(cancellationToken);

        public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => inner.Schema(cancellationToken);
    }

    private static WorkflowEventRecord Moved(string to) =>
        new("T-1/step/1", EventTypes.WorkflowTransition, "T-1", "produce", to, "step.completed", "done", "1.1");

    private static async Task<(RecordEventActivities Activities, ILedgerStore Ledger)> Open(Func<ILedgerStore, ILedgerStore>? wrap = null, IRedactor? redactor = null, ILogger<RecordEventActivities>? logger = null)
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var probe = new FakeProcessProbe();
        var store = new InMemorySindriStore(clock, probe);
        var held = new HeldLease();
        held.Hold((await store.Acquire(probe.Self(), TestContext.Current.CancellationToken)).Value);
        var ids = new SequentialIdGenerator();
        var events = new EventFactory(EventScope.ControlPlane, clock, ids, SliceHarness.Json, new AsyncLocalCallerContextAccessor(ids));
        return (new RecordEventActivities(wrap?.Invoke(store) ?? store, held, events, logger, redactor), store);
    }
}
