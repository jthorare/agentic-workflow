using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>When failure and profile rows appear, stay and clear (P4 plan Unit C; spec signals S5 to S7).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouClearingTests
{
    private static readonly DateTimeOffset _t = KnownInstants.Epoch;
    private static readonly ServiceName _service = ServiceName.From("heartbeat");
    private static readonly ProfileHash _approved = ProfileHash.From(new string('a', 64));
    private static readonly ProfileHash _live = ProfileHash.From(new string('b', 64));
    private readonly PassThroughScrubber _plain = new();

    [Fact]
    public void A_failed_run_adds_the_row_at_the_run_time()
    {
        var row = NeedsYouClearing.AfterRun(existing: null, Run(ServiceOutcome.Failed, _t), _plain).Value!;

        Expect.Equal((NeedsYouRowId.From("failure:service:heartbeat"), _t), (row.Id, row.Since));
    }

    [Fact]
    public void Failure_row_keeps_first_since()
    {
        var first = NeedsYouClearing.AfterRun(existing: null, Run(ServiceOutcome.Failed, _t), _plain).Value!;

        var again = NeedsYouClearing.AfterRun(first, Run(ServiceOutcome.Failed, _t.AddMinutes(10)), _plain).Value!;

        Expect.Equal(_t, again.Since);
    }

    [Fact]
    public void Ok_run_clears_failure()
    {
        var failing = NeedsYouClearing.AfterRun(existing: null, Run(ServiceOutcome.Failed, _t), _plain).Value;

        Expect.True(NeedsYouClearing.AfterRun(failing, Run(ServiceOutcome.Ok, _t.AddMinutes(1)), _plain).Value is null);
    }

    [Fact]
    public void An_ok_run_with_no_failure_row_stays_without_one() =>
        Expect.True(NeedsYouClearing.AfterRun(existing: null, Run(ServiceOutcome.Ok, _t), _plain).Value is null);

    [Fact]
    public void Drift_adds_profile_row_and_revert_clears_it()
    {
        var drifted = NeedsYouClearing.AfterCheck(existing: null, Profile(drifted: true, approved: [_service]), _plain).Value!;
        var again = NeedsYouClearing.AfterCheck(drifted, Profile(drifted: true, approved: [_service], at: _t.AddMinutes(5)), _plain).Value!;
        var reverted = NeedsYouClearing.AfterCheck(again, Profile(drifted: false, approved: [_service], at: _t.AddMinutes(9)), _plain);

        Expect.Equal((NeedsYouRowId.From("profile:drift"), _t), (drifted.Id, drifted.Since));
        Expect.Equal(drifted.Since, again.Since);   // since stays the first drift's
        Expect.True(reverted.Value is null);
    }

    [Fact]
    public void Approving_the_drift_clears_the_row()
    {
        var drifted = NeedsYouClearing.AfterCheck(existing: null, Profile(drifted: true, approved: [_service]), _plain).Value;

        Expect.True(NeedsYouClearing.AfterCheck(drifted, Profile(drifted: false, approved: [_service]) with { Approved = _live }, _plain).Value is null);
    }

    [Fact]
    public void Approved_profile_without_the_service_clears_failure()
    {
        var failing = NeedsYouClearing.AfterRun(existing: null, Run(ServiceOutcome.Failed, _t), _plain).Value!;

        Expect.True(NeedsYouClearing.FailureStands(failing, Profile(drifted: false, approved: [_service])));
        Expect.True(!NeedsYouClearing.FailureStands(failing, Profile(drifted: false, approved: [ServiceName.From("index")])));
    }

    [Fact]
    public void A_built_in_failure_stands_at_a_check_whose_profile_does_not_name_it()
    {
        var failing = NeedsYouClearing.AfterRun(existing: null, Run(ServiceOutcome.Failed, _t), _plain, builtIn: true).Value!;

        Expect.True(NeedsYouClearing.FailureStands(failing, Profile(drifted: false, approved: [ServiceName.From("index")]), [_service]));
        Expect.True(!NeedsYouClearing.FailureStands(failing, Profile(drifted: false, approved: [ServiceName.From("index")]), [ServiceName.From("other")]));
    }

    [Fact]
    public void A_failure_stands_while_the_removal_is_unapproved_or_nothing_is_approved()
    {
        var failing = NeedsYouClearing.AfterRun(existing: null, Run(ServiceOutcome.Failed, _t), _plain).Value!;

        // Drifted: the approved profile still names the service (the check reports the approved list), so the row stays.
        Expect.True(NeedsYouClearing.FailureStands(failing, Profile(drifted: true, approved: [_service])));
        // No approval at all: nothing says the service was removed.
        Expect.True(NeedsYouClearing.FailureStands(failing, Profile(drifted: false, approved: []) with { Approved = null }));
    }

    private static ServiceRow Run(ServiceOutcome outcome, DateTimeOffset at) =>
        new(_service, new LastRun(at, outcome, outcome == ServiceOutcome.Failed ? "SND-SCHED-001" : null, at), Paused: false, Sequence: 1);

    private static ProfileRow Profile(bool drifted, ServiceName[] approved, DateTimeOffset? at = null) =>
        new(_approved, _t, _live, at ?? _t, drifted, drifted ? ["services.heartbeat.every"] : [], approved, Sequence: 2);
}
