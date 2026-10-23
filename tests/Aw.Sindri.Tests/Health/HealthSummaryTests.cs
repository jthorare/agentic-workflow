using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.Health;

/// <summary>The health word, its problems and the served sentence (UI spec E-14; API spec §2.3; plan unit E).</summary>
public sealed class HealthSummaryTests
{
    private static readonly DateTimeOffset _slot = new(2026, 10, 14, 9, 30, 0, TimeSpan.Zero);

    private static ViewsStatus Fresh() => new(Cursor: 9, LedgerHead: 9, _slot, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null);

    private static ViewsStatus Stalled() => new(Cursor: 6, LedgerHead: 9, _slot, Rebuilding: false, Stalled: true, StalledAt: 7, StalledType: "service.ran");

    private static ViewsStatus Rebuilding() => new(Cursor: 3, LedgerHead: 9, _slot, Rebuilding: true, Stalled: false, StalledAt: null, StalledType: null);

    private static ServiceRow Row(string name, ServiceOutcome outcome, string? code = null) =>
        new(ServiceName.From(name), new LastRun(_slot, outcome, code, _slot), Paused: false, Sequence: 1);

    private static HealthReport For(bool running = true, ViewsStatus? views = null, IReadOnlyList<ServiceRow>? services = null,
        bool needsApproval = false, bool changed = false, IReadOnlyList<HealthProblem>? codeIndex = null) =>
        HealthSummary.For(running, views ?? Fresh(), services ?? [], changed ? ProfileApproval.Changed : needsApproval ? ProfileApproval.NeedsApproval : ProfileApproval.Approved, codeIndex);

    [Fact, Trait("Lane", "unit")]
    public void Nothing_wrong_is_healthy_with_no_problems_and_no_sentence()
    {
        var health = For(services: [Row("heartbeat", ServiceOutcome.Ok)]);

        Assert.Equal(HealthWord.Healthy, health.Word);
        Assert.Empty(health.Problems);
        Assert.Null(health.Sentence);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, false, false, false, HealthWord.Degraded)]
    [InlineData(true, true, false, false, HealthWord.Stalled)]
    [InlineData(true, false, true, false, HealthWord.Rebuilding)]
    [InlineData(true, true, true, true, HealthWord.Stalled)]
    [InlineData(true, false, true, true, HealthWord.Rebuilding)]
    [InlineData(true, false, false, true, HealthWord.Degraded)]
    public void Health_word_follows_the_precedence(bool running, bool stalled, bool rebuilding, bool failedService, HealthWord expected)
    {
        var views = stalled ? Stalled() : rebuilding ? Rebuilding() : Fresh();
        IReadOnlyList<ServiceRow> services = failedService ? [Row("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001")] : [];

        Assert.Equal(expected, For(running, views, services).Word);
    }

    [Fact, Trait("Lane", "unit")]
    public void Code_index_problems_follow_the_services_and_come_before_the_profile()
    {
        HealthProblem[] index = [new("SND-CODEIDX-014", "demo: code index is behind main; rebuild it from the System page", "the code index for demo is behind main"), new("SND-CODEIDX-010", "other: code index is damaged; it rebuilds on the next check", "the code index for other is damaged")];

        var health = For(services: [Row("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001")], needsApproval: true, codeIndex: index);

        Assert.Equal(["SND-SCHED-001", "SND-CODEIDX-014", "SND-CODEIDX-010", "SND-PROFILE-005"], health.Problems.Select(p => p.Code), StringComparer.Ordinal);
        Assert.Equal(HealthWord.Degraded, health.Word);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_code_index_problem_alone_degrades_health_and_reads_in_the_sentence()
    {
        var health = For(codeIndex: [new("SND-CODEIDX-014", "demo: code index is behind main; rebuild it from the System page", "the code index for demo is behind main")]);

        Assert.Equal("Sindri is degraded: the code index for demo is behind main.", health.Sentence);
    }

    [Fact, Trait("Lane", "unit")]
    public void Health_lists_problems_in_order()
    {
        var health = For(running: false, Stalled(),
            [Row("digest", ServiceOutcome.Ok), Row("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001"), Row("purge", ServiceOutcome.Failed, "SND-CTX-001")],
            needsApproval: true);

        Assert.Equal(["SND-API-001", "SND-VIEW-001", "SND-SCHED-001", "SND-CTX-001", "SND-PROFILE-005"], health.Problems.Select(p => p.Code), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_problem_has_its_fix_text_and_its_clause()
    {
        var health = For(running: false, Stalled(), [Row("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001")], needsApproval: true);

        Assert.Equal(
            [
                ("SND-API-001", "Sindri isn't running: run `sindri`", "Sindri isn't running"),
                ("SND-VIEW-001", "Views are stalled at 7: run `sindri views rebuild`", "the views stopped at event 7"),
                ("SND-SCHED-001", "Service heartbeat failed at 2026-10-14T09:30:00Z", "service heartbeat failed"),
                ("SND-PROFILE-005", "Not approved: run `sindri profile approve`", "the profile isn't approved"),
            ],
            health.Problems.Select(p => (p.Code, p.Text, p.Clause)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rebuild_names_the_cursor_and_a_changed_profile_beats_needs_approval()
    {
        var health = For(views: Rebuilding(), needsApproval: true, changed: true);

        Assert.Equal(
            [
                ("SND-API-010", "Rebuilding views (cursor 3 of 9)…", "the views are replaying (cursor 3 of 9)"),
                ("SND-PROFILE-003", "The live profile changed since approval: run `sindri profile approve`", "the profile changed since approval"),
            ],
            health.Problems.Select(p => (p.Code, p.Text, p.Clause)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Stalled_views_report_the_stall_not_the_rebuild()
    {
        var views = Stalled() with { Rebuilding = true };

        Assert.Equal(["SND-VIEW-001"], For(views: views).Problems.Select(p => p.Code), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_sentence_joins_the_clauses_with_an_oxford_comma()
    {
        var one = For(views: Stalled());
        var two = For(running: false, services: [Row("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001")]);
        var three = For(services: [Row("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001")], changed: true, needsApproval: true);
        var four = For(running: false, services: [Row("heartbeat", ServiceOutcome.Failed, "SND-SCHED-001")], needsApproval: true);

        Assert.Equal("Sindri is stalled: the views stopped at event 7.", one.Sentence);
        Assert.Equal("Sindri is degraded: Sindri isn't running and service heartbeat failed.", two.Sentence);
        Assert.Equal("Sindri is degraded: service heartbeat failed and the profile changed since approval.", three.Sentence);
        Assert.Equal("Sindri is degraded: Sindri isn't running, service heartbeat failed, and the profile isn't approved.", four.Sentence);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rebuild_sentence_names_the_word()
        => Assert.Equal("Sindri is rebuilding: the views are replaying (cursor 3 of 9).", For(views: Rebuilding()).Sentence);

    [Fact, Trait("Lane", "unit")]
    public void A_failed_service_without_a_recorded_code_uses_the_scheduler_code()
    {
        var problem = Assert.Single(For(services: [Row("heartbeat", ServiceOutcome.Failed)]).Problems);

        Assert.Equal("SND-SCHED-001", problem.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Views_that_cannot_be_updated_name_no_event_and_the_ledger_is_fixed_by_the_doctor()
    {
        var failing = Fresh() with { Stalled = true, StalledType = "SND-VIEW-001: views.db can't be opened" };
        var ledger = Fresh() with { Stalled = true, StalledType = "SND-LEDGER-002: the ledger can't be read" };

        Assert.Equal(
            [("SND-VIEW-001", "The views can't be updated (SND-VIEW-001: views.db can't be opened): run `sindri views rebuild`", "the views can't be updated")],
            For(views: failing).Problems.Select(p => (p.Code, p.Text, p.Clause)));
        Assert.Equal("Sindri is stalled: the views can't be updated.", For(views: failing).Sentence);
        Assert.Equal(
            "The views can't be updated (SND-LEDGER-002: the ledger can't be read): run `sindri doctor`",
            Assert.Single(For(views: ledger).Problems).Text);
        Assert.Equal(failing.Text, Assert.Single(For(views: failing).Problems).Text);
    }
}
