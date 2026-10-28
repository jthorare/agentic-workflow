using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'needs_you' fold (P4 plan Unit C; spec signals S1, S3, S5 to S7; lead 2026-10-14: a failure row clears on the first idle ok run).</summary>
public sealed class NeedsYouProjectionTests
{
    private static readonly ServiceName _heartbeat = ServiceName.From("heartbeat");
    private static readonly ServiceName _index = ServiceName.From("index");
    private static readonly ProfileHash _approved = ProfileHash.From(new string('a', 64));
    private static readonly ProfileHash _live = ProfileHash.From(new string('b', 64));
    private static readonly NeedsYouRowId _drift = NeedsYouRowId.From("profile:drift");
    private static readonly NeedsYouRowId _failure = NeedsYouRowId.From("failure:service:heartbeat");
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness H => _v.Slice;

    [Fact, Trait("Lane", "unit")]
    public async Task Filed_request_is_one_decision_row()
    {
        await File(A.FirstRequestId, A.FirstDecisionId, "Split the work.");

        var rows = await Rows();

        var record = Assert.Single(rows);
        Assert.Equal(NeedsYouRowId.From("decision:" + A.FirstDecisionId.Value), record.Row.Id);
        Assert.Equal((NeedsYouKind.Decision, Urgency.Pulse, true, "Split the work.", "decision · re-scope"), (record.Row.Kind, record.Row.Urgency, record.Row.RecordedOnly, record.Row.Title, record.Row.Label));
        Assert.Equal(1L, record.Sequence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_clears_its_row()
    {
        var second = DecisionId.From("DEC-00000000000000000000000000000002");
        await File(A.FirstRequestId, A.FirstDecisionId, "one");
        await File(RequestId.From("REQ-00000000000000000000000000000002"), second, "two");

        await _v.Fold(EventTypes.DecisionRecorded, new DecisionRecorded(H.Ids.New<CommandId>(), A.FirstDecisionId, DecisionOption.From("accept"), Note: null, "human:ui"));

        Assert.Equal(["decision:" + second.Value], (await Rows()).Select(r => r.Row.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Closing_the_item_clears_its_decision_rows_and_only_those()
    {
        var other = ItemId.From("ITM-00000000000000000000000000000002");
        var second = DecisionId.From("DEC-00000000000000000000000000000002");
        await _v.Fold(EventTypes.ItemCreated, new ItemCreated(H.Ids.New<CommandId>(), A.FirstItemId, "t", Repo: null, Link: null, Body: null, Owner: null), A.FirstItemId);
        await _v.Fold(EventTypes.ItemCreated, new ItemCreated(H.Ids.New<CommandId>(), other, "t", Repo: null, Link: null, Body: null, Owner: null), other);
        await File(A.FirstRequestId, A.FirstDecisionId, "one", A.FirstItemId);
        await File(RequestId.From("REQ-00000000000000000000000000000002"), second, "two", other);
        await Ran(_heartbeat, ServiceOutcome.Failed);

        await _v.Fold(EventTypes.ItemStateChanged, new ItemStateChanged(H.Ids.New<CommandId>(), A.FirstItemId, ItemState.NeedsDecision, ItemState.Done, 2, Reason: null), A.FirstItemId);

        Assert.Equal([_failure.Value, "decision:" + second.Value], (await Rows()).Select(r => r.Row.Id.Value).Order(StringComparer.Ordinal).Reverse(), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_run_adds_a_failure_row()
    {
        await Ran(_heartbeat, ServiceOutcome.Failed, "SND-SCHED-001");

        var record = Assert.Single(await Rows());

        Assert.Equal((_failure, NeedsYouKind.Failure, Urgency.Colour, "Service heartbeat failed", "SND-SCHED-001: A background service failed"), (record.Row.Id, record.Row.Kind, record.Row.Urgency, record.Row.Title, record.Row.Reason));
        Assert.Equal(H.Clock.GetUtcNow(), record.Row.Since);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Failure_row_keeps_first_since_in_the_view()
    {
        await Ran(_heartbeat, ServiceOutcome.Failed, "SND-SCHED-001");
        var first = H.Clock.GetUtcNow();
        H.Clock.Advance(TimeSpan.FromMinutes(5));

        await Ran(_heartbeat, ServiceOutcome.Failed, "SND-SCHED-001");

        var record = Assert.Single(await Rows());
        Assert.Equal(first, record.Row.Since);
        Assert.Equal(2L, record.Sequence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Ok_run_clears_failure_in_the_view()
    {
        await Ran(_heartbeat, ServiceOutcome.Failed, "SND-SCHED-001");
        await Ran(_index, ServiceOutcome.Failed, "SND-SCHED-001");

        await Ran(_heartbeat, ServiceOutcome.Ok);

        Assert.Equal(["failure:service:index"], (await Rows()).Select(r => r.Row.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Manual_ok_run_clears_the_failure_row_in_the_view()
    {
        await Ran(_heartbeat, ServiceOutcome.Failed, "SND-SCHED-001");

        await _v.Fold(EventTypes.ServiceRan, new ServiceRan(H.Ids.New<CommandId>(), _heartbeat, H.Clock.GetUtcNow(), ServiceOutcome.Ok, Code: null, A.FirstJobId));

        Assert.Empty(await Rows());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_ok_run_with_no_failure_row_adds_nothing()
    {
        await Ran(_heartbeat, ServiceOutcome.Ok);

        Assert.Empty(await Rows());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Drift_adds_profile_row_and_revert_clears_it_in_the_view()
    {
        await Check(drifted: true, "services.heartbeat.every");
        var first = H.Clock.GetUtcNow();
        var added = Assert.Single(await Rows());
        H.Clock.Advance(TimeSpan.FromSeconds(30));

        await Check(drifted: true, "services.heartbeat.every", "services.index");
        var again = Assert.Single(await Rows());

        H.Clock.Advance(TimeSpan.FromSeconds(30));
        await Check(drifted: false);

        Assert.Equal((_drift, NeedsYouKind.Profile, Urgency.Calm, SubjectKind.Profile, _live.Value), (added.Row.Id, added.Row.Kind, added.Row.Urgency, added.Row.Subject.Kind, added.Row.Subject.Id.Value));
        Assert.Equal((first, "1 key path changed: services.heartbeat.every"), (added.Row.Since, added.Row.Reason));
        Assert.Equal((first, "2 key paths changed: services.heartbeat.every, services.index"), (again.Row.Since, again.Row.Reason));
        Assert.Empty(await Rows());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_the_live_hash_clears_the_profile_row()
    {
        await Check(drifted: true, "services.index");

        await _v.Fold(EventTypes.ProfileApproved, new ProfileApproved(_live, ["services.index"]));

        Assert.Empty(await Rows());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_some_other_hash_leaves_the_profile_row()
    {
        await Check(drifted: true, "services.index");

        await _v.Fold(EventTypes.ProfileApproved, new ProfileApproved(_approved, []));

        Assert.Equal([_drift.Value], (await Rows()).Select(r => r.Row.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approved_profile_without_the_service_clears_failure_in_the_view()
    {
        await Ran(_heartbeat, ServiceOutcome.Failed, "SND-SCHED-001");
        await Ran(_index, ServiceOutcome.Failed, "SND-SCHED-001");

        await Check(drifted: false, approvedServices: [_index]);   // heartbeat was removed from the profile and the profile approved

        Assert.Equal(["failure:service:index"], (await Rows()).Select(r => r.Row.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failure_stays_while_the_removal_is_only_a_drift()
    {
        await Ran(_heartbeat, ServiceOutcome.Failed, "SND-SCHED-001");

        await Check(drifted: true, approvedServices: [_heartbeat], paths: "services.heartbeat");

        Assert.Equal([_failure.Value, _drift.Value], (await Rows()).Select(r => r.Row.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rows_filter_by_kind()
    {
        await File(A.FirstRequestId, A.FirstDecisionId, "one");
        await Ran(_heartbeat, ServiceOutcome.Failed, "SND-SCHED-001");

        var failures = (await _v.Views.Where<NeedsYouRowRecord>(ViewNames.NeedsYou, ViewColumn.Kind, "failure", Token)).Value;

        Assert.Equal([_failure.Value], failures.Select(r => r.Row.Id.Value), StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<NeedsYouRowRecord>> Rows() => (await _v.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, Token)).Value;

    private Task File(RequestId request, DecisionId decision, string text, ItemId? item = null) =>
        item is { } named
            ? _v.Fold(EventTypes.RequestFiled, new RequestFiled(H.Ids.New<CommandId>(), request, decision, RequestKind.Rescope, named, Repo: null, text), named)
            : _v.Fold(EventTypes.RequestFiled, new RequestFiled(H.Ids.New<CommandId>(), request, decision, RequestKind.Rescope, Item: null, Repo: null, text));

    private Task Ran(ServiceName service, ServiceOutcome outcome, string? code = null) =>
        _v.Fold(EventTypes.ServiceRan, new ServiceRan(H.Ids.New<CommandId>(), service, H.Clock.GetUtcNow(), outcome, code));

    private Task Check(bool drifted, params string[] paths) => Check(drifted, [_heartbeat, _index], paths);

    private Task Check(bool drifted, ServiceName[] approvedServices, params string[] paths) =>
        _v.Fold(EventTypes.ProfileChecked, new ProfileChecked(H.Ids.New<CommandId>(), _approved, _live, drifted, paths, approvedServices));
}
