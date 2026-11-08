using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// What a linked instance (a phase, LN-WF1) tells its line: an engine-origin 'effect.line.report' for a park, a resume and an end, with
/// one identity per change; an instance that is not linked, or has no line context, tells nobody.
/// </summary>
public sealed class LinkedPhaseReportsTests
{
    private static readonly BlockId _report = BlockId.From("effect.line.report");

    [Fact, Trait("Lane", "unit")]
    public void A_park_reports_parked_with_its_reason()
    {
        var open = Linked();
        var parked = Suspensions.Suspend(open, Park(ParkReason.Budget));

        var command = LinkedPhaseReports.For(open, parked, version: 4);

        Assert.NotNull(command);
        Assert.Equal((_report, CommandOrigin.Engine), (command.Effect, command.Origin));
        Assert.Equal("parked", Text(command, "status"));
        Assert.Equal("budget", Text(command, "parkReason"));
        Assert.Equal(FactKey.From("fact:lineContext"), command.With["line"].Fact);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_report_passes_the_phase_packet_so_the_plans_phase_id_is_read_from_it()
    {
        var open = Linked();

        var command = LinkedPhaseReports.For(open, Suspensions.Suspend(open, Park(ParkReason.Budget)), version: 4)!;

        Assert.Equal(FactKey.From("fact:phasePacket"), command.With["phasePacket"].Fact);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_resume_reports_resumed()
    {
        var open = Linked();
        var parked = Suspensions.Suspend(open, Park(ParkReason.NeedsAnswer));

        Assert.Equal("resumed", Text(LinkedPhaseReports.For(parked, open, version: 5)!, "status"));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_end_reports_the_outcome_and_the_packet_the_phase_started_with()
    {
        var open = Linked();
        var ended = open with { Outcome = OutcomeName.From("resolved") };

        var command = LinkedPhaseReports.For(open, ended, version: 9)!;

        Assert.Equal("ended", Text(command, "status"));
        Assert.Equal("resolved", Text(command, "ended"));
        Assert.Equal(open.Facts[FactKey.From("fact:phasePacket")].Sha256, Text(command, "packet"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cancelled_end_reports_cancelled_so_the_line_counts_the_phase_failed()
    {
        var open = Linked();

        var command = LinkedPhaseReports.For(open, open with { Outcome = OutcomeName.From("cancelled") }, version: 9)!;

        Assert.Equal("cancelled", Text(command, "ended"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_change_has_its_own_identity_so_a_phase_that_parks_twice_reports_twice()
    {
        var open = Linked();
        var parked = Suspensions.Suspend(open, Park(ParkReason.Rounds));

        var first = LinkedPhaseReports.For(open, parked, version: 4)!;
        var second = LinkedPhaseReports.For(open, parked, version: 8)!;

        Assert.NotEqual(first.Identity.Text, second.Identity.Text, StringComparer.Ordinal);
        Assert.NotEqual(EngineNotices.For(open, parked, version: 4, deskTaskNotified: true).Single().Command.Identity.Text, first.Identity.Text, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_park_that_stays_parked_and_an_unchanged_instance_report_nothing()
    {
        var open = Linked();
        var parked = Suspensions.Suspend(open, Park(ParkReason.Rounds));

        Assert.Null(LinkedPhaseReports.For(parked, parked, version: 5));
        Assert.Null(LinkedPhaseReports.For(open, open, version: 5));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_instance_that_is_not_linked_or_has_no_line_context_reports_nothing()
    {
        var root = At(Step, "produce");
        var linkedWithoutLine = Linked() with { Facts = new Dictionary<FactKey, FactValue>() };

        Assert.Null(LinkedPhaseReports.For(root, Suspensions.Suspend(root, Park(ParkReason.Rounds)), version: 2));
        Assert.Null(LinkedPhaseReports.For(linkedWithoutLine, Suspensions.Suspend(linkedWithoutLine, Park(ParkReason.Rounds)), version: 2));
    }

    private static WorkflowInstance Linked() => At(Step, "produce") with
    {
        Address = InstanceAddress.From("T-3/line/1/phase.p2.1"),
        Facts = new Dictionary<FactKey, FactValue>
        {
            [FactKey.From("fact:lineContext")] = Fact("""{"line":"L1","phase":"p2"}"""),
            [FactKey.From("fact:phasePacket")] = Fact("packet"),
        },
    };

    private static string Text(EffectCommand command, string name) => command.With[name].Literal!.Value.GetString()!;

    private static Suspension Park(ParkReason reason) =>
        new(SuspensionKind.Park, reason, ParkKind: null, Escalation: null, "q?", "system:timer", Mirrors: null, new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effects: []));
}
