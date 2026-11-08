using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;
using RecipeUrgency = Aw.Sindri.Core.Workflow.Recipes.RecipeUrgency;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>The order of an instance list (API spec §3.8): the four keys in turn, then the address; one test per key.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceOrderTests
{
    private static InstanceView Row(string name, string address, RecipeUrgency urgency = RecipeUrgency.Work, string? label = null) =>
        StatusCases.View(name) with
        {
            Address = InstanceAddress.From(address),
            Urgency = urgency,
            Subject = label is null ? null : new SubjectRef("file", "fact:spec", label, Href: null),
        };

    private static string[] Sorted(params InstanceView[] views) =>
        [.. views.Order(new InstanceOrder(ItemTitles.None)).Select(static v => v.Address.Value)];

    [Fact]
    public void Key_1_unfinished_rows_come_before_finished_ones_whatever_their_urgency() =>
        Assert.Equal(["T-2/step/1", "T-1/step/1"], Sorted(
            Row("done", "T-1/step/1", RecipeUrgency.Incident),
            Row("desk-task", "T-2/step/1", RecipeUrgency.Work)));

    [Fact]
    public void Key_1_a_cancelled_instance_is_finished_too() =>
        Assert.Equal(["T-2/step/1", "T-1/step/1"], Sorted(Row("cancelled", "T-1/step/1"), Row("desk-task", "T-2/step/1")));

    [Fact]
    public void Key_2_parked_and_escalated_rows_come_after_the_others_whatever_their_urgency() =>
        Assert.Equal(["T-3/step/1", "T-4/step/1", "T-1/step/1", "T-2/step/1"], Sorted(
            Row("parked-rounds", "T-1/step/1", RecipeUrgency.Incident),
            Row("escalated-gate", "T-2/step/1", RecipeUrgency.Incident),
            Row("desk-task", "T-3/step/1"),
            Row("taken-over", "T-4/step/1")));

    [Fact]
    public void Key_2_a_desk_task_and_a_taken_over_row_are_neither_parked_nor_escalated_so_urgency_orders_them() =>
        Assert.Equal(["T-2/step/1", "T-1/step/1"], Sorted(
            Row("desk-task", "T-1/step/1"),
            Row("taken-over", "T-2/step/1", RecipeUrgency.Triage)));

    [Fact]
    public void Key_3_urgency_is_incident_then_triage_then_work() =>
        Assert.Equal(["T-3/step/1", "T-2/step/1", "T-1/step/1"], Sorted(
            Row("desk-task", "T-1/step/1"),
            Row("desk-task", "T-2/step/1", RecipeUrgency.Triage),
            Row("desk-task", "T-3/step/1", RecipeUrgency.Incident)));

    [Fact]
    public void Key_4_subject_label_breaks_a_tie_by_ordinal_comparison() =>
        Assert.Equal(["T-3/step/1", "T-2/step/1", "T-1/step/1"], Sorted(
            Row("desk-task", "T-1/step/1", label: "b"),
            Row("desk-task", "T-2/step/1", label: "a"),
            Row("desk-task", "T-3/step/1", label: "A")));

    [Fact]
    public void Key_4_an_instance_with_no_subject_sorts_by_the_label_its_row_shows_which_is_its_address() =>
        Assert.Equal(["T-2/step/1", "T-1/step/1", "T-3/step/1"], Sorted(
            Row("desk-task", "T-3/step/1", label: "U"),
            Row("desk-task", "T-1/step/1"),
            Row("desk-task", "T-2/step/1", label: "A")));   // 'A' < 'T-1/step/1' < 'U': an empty label would have put the subject-less row first

    [Fact]
    public void The_address_is_the_final_tiebreak_by_ordinal_comparison() =>
        Assert.Equal(["T-1/step/1", "T-10/step/1", "T-2/step/1"], Sorted(
            Row("desk-task", "T-2/step/1"),
            Row("desk-task", "T-10/step/1"),
            Row("desk-task", "T-1/step/1")));

    [Fact]
    public void The_start_time_and_the_time_in_state_do_not_order_rows() =>
        Assert.Equal(["T-1/step/1", "T-2/step/1"], Sorted(
            Row("desk-task", "T-2/step/1") with { Since = StatusCases.Now.AddDays(-9) },
            Row("desk-task", "T-1/step/1") with { Since = StatusCases.Now }));

    [Fact]
    public void An_earlier_key_beats_a_later_one() =>
        Assert.Equal(["T-2/step/1", "T-1/step/1"], Sorted(
            Row("desk-task", "T-1/step/1", label: "a"),
            Row("desk-task", "T-2/step/1", RecipeUrgency.Incident, label: "z")));
}
